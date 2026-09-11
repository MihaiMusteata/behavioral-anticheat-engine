using System.Security.Cryptography;
using System.Text.Json;
using BehavioralAnticheatEngine.Application.Auth;
using BehavioralAnticheatEngine.Application.Common.Exceptions;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Dashboard.Contracts;
using BehavioralAnticheatEngine.Application.Exams.Contracts;
using BehavioralAnticheatEngine.Domain.Exams;

namespace BehavioralAnticheatEngine.Application.Exams;

public sealed class ExamSessionService : IExamSessionService
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    // Generous for any realistic exam answer (free_text essays included - this is
    // roughly 10-15 pages of text) while still rejecting pathological/abusive
    // payloads that would otherwise cost real CPU/DB time on an already
    // resource-constrained server with no other size limit in front of it.
    private const int MaxAnswerJsonLength = 20_000;

    private readonly IExamRepository _exams;
    private readonly IAuthService _authService;
    private readonly IExamSessionSecretStore _sessionSecrets;
    private readonly ILiveExamStateStore _liveState;
    private readonly IDashboardEventBus _dashboardEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ExamSessionService(
        IExamRepository exams,
        IAuthService authService,
        IExamSessionSecretStore sessionSecrets,
        ILiveExamStateStore liveState,
        IDashboardEventBus dashboardEvents,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _exams = exams;
        _authService = authService;
        _sessionSecrets = sessionSecrets;
        _liveState = liveState;
        _dashboardEvents = dashboardEvents;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<GuestJoinResponse> JoinAsGuestAsync(JoinExamGuestRequest request, CancellationToken cancellationToken = default)
    {
        var pinCode = (request.PinCode ?? string.Empty).Trim();
        if (pinCode.Length == 0)
        {
            throw new ApplicationValidationException("Access code is required.");
        }

        if (!request.ConsentAccepted)
        {
            throw new ApplicationValidationException("You must accept the participation terms and data processing notice to join.");
        }

        var schedule = await _exams.GetExamScheduleByPinCodeAsync(pinCode, includeAssessment: true, cancellationToken)
            ?? throw new ApplicationNotFoundException("Invalid access code.");

        var now = _timeProvider.GetUtcNow();
        if (!schedule.CanJoin(now))
        {
            throw new ApplicationForbiddenException("This exam is not open right now.");
        }

        var auth = await _authService.JoinAsGuestAsync(request.ParticipantName, cancellationToken);
        var session = await StartSessionAsync(schedule.Id, auth.UserId, request.ParticipantName, now, cancellationToken);

        return new GuestJoinResponse(
            auth.UserId,
            auth.AccessToken,
            auth.AccessTokenExpiresAtUtc,
            auth.RefreshToken,
            auth.RefreshTokenExpiresAtUtc,
            session);
    }

    public Task<StartExamSessionResponse> StartSessionAsync(Guid examScheduleId, Guid studentId, CancellationToken cancellationToken = default)
    {
        return StartSessionAsync(examScheduleId, studentId, participantName: null, _timeProvider.GetUtcNow(), cancellationToken);
    }

    private async Task<StartExamSessionResponse> StartSessionAsync(
        Guid examScheduleId,
        Guid studentId,
        string? participantName,
        DateTimeOffset consentAcceptedAtUtc,
        CancellationToken cancellationToken)
    {
        var schedule = await _exams.GetExamScheduleAsync(examScheduleId, includeAssessment: true, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam schedule was not found.");

        var now = _timeProvider.GetUtcNow();
        if (!schedule.CanJoin(now))
        {
            throw new ApplicationForbiddenException("This exam is not open right now.");
        }

        // CanJoin() only checks Status == Running, not the time window - a schedule left
        // "Running" past its own EndsAtUtc (proctor forgot to Stop it) would otherwise let a
        // brand-new guest "successfully" join and then immediately hit "Session has expired"
        // on the very next call, since a freshly created session already inherits that past
        // EndsAtUtc. Reject at the join step instead, with the same message CanJoin() already
        // uses, so the failure is clear immediately rather than one request later.
        if (schedule.EndsAtUtc is not null && schedule.EndsAtUtc.Value <= now)
        {
            throw new ApplicationForbiddenException("This exam is not open right now.");
        }

        if (schedule.Assessment is null || schedule.Assessment.Questions.Count == 0)
        {
            throw new ApplicationValidationException("Exam has no questions.");
        }

        var session = await _exams.GetActiveSessionAsync(examScheduleId, studentId, cancellationToken);
        if (session is not null && session.EndsAtUtc <= now)
        {
            // The session's time ran out without the client ever calling submit (e.g. the tab
            // was closed, or the previous submit attempt failed). Finalize it now so it stops
            // being reported as "active" and resumable on every subsequent refresh.
            session.Submit(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ApplicationValidationException("Session has expired.");
        }

        if (session is null)
        {
            var trimmedName = (participantName ?? string.Empty).Trim();
            if (trimmedName.Length == 0)
            {
                throw new ApplicationNotFoundException("Exam session was not found.");
            }

            var endsAtUtc = schedule.EndsAtUtc!.Value;
            session = ExamSession.Start(schedule.Id, schedule.AssessmentId, studentId, trimmedName, now, endsAtUtc, consentAcceptedAtUtc);
            await _exams.AddSessionAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var secret = await _sessionSecrets.GetSecretAsync(session.Id, cancellationToken);
        if (secret is null)
        {
            secret = CreateSessionSecret();
            await _sessionSecrets.StoreSecretAsync(
                session.Id,
                secret,
                session.EndsAtUtc - now > TimeSpan.Zero ? session.EndsAtUtc - now : TimeSpan.FromMinutes(5),
                cancellationToken);
        }

        await PublishLiveStartAsync(session, cancellationToken);

        var existingAnswers = await _exams.ListAnswersForSessionAsync(session.Id, cancellationToken);

        return new StartExamSessionResponse(
            session.Id,
            schedule.Id,
            schedule.AssessmentId,
            session.ParticipantName,
            session.EndsAtUtc,
            now,
            secret,
            ToStudentAssessment(schedule.Assessment),
            existingAnswers.Select(answer => new ExistingAnswerDto(answer.QuestionId, answer.AnswerJson)).ToArray());
    }

    public async Task<SaveAnswerResponse> SaveAnswerAsync(Guid sessionId, Guid studentId, SaveAnswerRequest request, CancellationToken cancellationToken = default)
    {
        var session = await _exams.GetSessionWithAssessmentAsync(sessionId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam session was not found.");
        EnsureStudentOwnsSession(session, studentId);
        await EnsureSessionIsActiveAsync(session, cancellationToken);

        var question = session.Assessment?.Questions.FirstOrDefault(candidate => candidate.Id == request.QuestionId)
            ?? throw new ApplicationNotFoundException("Question was not found in this session.");
        var answerJson = request.Answer.GetRawText();
        if (answerJson.Length > MaxAnswerJsonLength)
        {
            throw new ApplicationValidationException(
                $"Answer is too long (max {MaxAnswerJsonLength:N0} characters).");
        }
        var existing = await _exams.GetAnswerAsync(sessionId, request.QuestionId, cancellationToken);
        var now = _timeProvider.GetUtcNow();

        if (existing is null)
        {
            existing = StudentAnswer.Create(sessionId, request.QuestionId, answerJson, question.RequiresManualScoring(), now);
            await _exams.AddAnswerAsync(existing, cancellationToken);
        }
        else
        {
            existing.Update(answerJson, question.RequiresManualScoring(), now);
        }

        var autoScore = TryScoreAutomatically(question, request.Answer);
        if (autoScore is not null)
        {
            existing.SetAutoScore(autoScore.Value);
        }

        session.MarkActivity(now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveAnswerResponse(question.Id, autoScore, question.RequiresManualScoring());
    }

    public async Task<SubmitExamResponse> SubmitAsync(Guid sessionId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var session = await _exams.GetSessionAsync(sessionId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam session was not found.");
        EnsureStudentOwnsSession(session, studentId);

        var now = _timeProvider.GetUtcNow();
        session.Submit(now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var answers = await _exams.ListAnswersForSessionAsync(sessionId, cancellationToken);
        var score = answers.All(answer => !answer.RequiresManualScoring || answer.ManualScore is not null)
            ? answers.Sum(answer => answer.ManualScore ?? answer.AutoScore ?? 0)
            : (decimal?)null;

        await PublishLiveStateAsync(session, "session.submitted", cancellationToken);

        return new SubmitExamResponse(session.Id, session.Status, now, score);
    }

    public async Task<bool> SelfReportCheatingAsync(Guid sessionId, Guid studentId, SelfReportCheatingRequest request, CancellationToken cancellationToken = default)
    {
        var session = await _exams.GetSessionAsync(sessionId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam session was not found.");
        EnsureStudentOwnsSession(session, studentId);

        session.RecordSelfReport(request.Cheated, _timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<SaveAnswerResponse> ScoreAnswerManuallyAsync(Guid sessionId, Guid questionId, ManualScoreRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Score < 0)
        {
            throw new ApplicationValidationException("Manual score cannot be negative.");
        }

        var answer = await _exams.GetAnswerAsync(sessionId, questionId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Answer was not found.");

        answer.SetManualScore(request.Score, request.Feedback);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveAnswerResponse(questionId, answer.AutoScore, answer.RequiresManualScoring);
    }

    private async Task PublishLiveStartAsync(ExamSession session, CancellationToken cancellationToken)
    {
        await PublishLiveStateAsync(session, "session.started", cancellationToken);
    }

    private async Task PublishLiveStateAsync(ExamSession session, string eventType, CancellationToken cancellationToken)
    {
        var existingState = (await _liveState.ListByExamAsync(session.ExamScheduleId, cancellationToken))
            .FirstOrDefault(state => state.SessionId == session.Id);
        var state = new LiveSessionState(
            session.ExamScheduleId,
            session.Id,
            session.StudentId,
            session.ParticipantName,
            session.Status,
            session.CurrentRiskScore,
            existingState?.TotalEvents ?? 0,
            session.LastActivityAtUtc,
            eventType);

        await _liveState.UpsertAsync(state, cancellationToken);
        await _dashboardEvents.PublishAsync(
            new DashboardLiveEvent(
                session.ExamScheduleId,
                session.Id,
                session.StudentId,
                eventType,
                JsonSerializer.Serialize(state, WebJsonOptions),
                _timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private static void EnsureStudentOwnsSession(ExamSession session, Guid studentId)
    {
        if (session.StudentId != studentId)
        {
            throw new ApplicationForbiddenException("Session does not belong to the current student.");
        }
    }

    private async Task EnsureSessionIsActiveAsync(ExamSession session, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        if (session.Status != ExamSessionStatuses.Active)
        {
            throw new ApplicationValidationException("Session is not active.");
        }

        if (session.EndsAtUtc <= now)
        {
            session.Submit(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ApplicationValidationException("Session has expired.");
        }
    }

    private static string CreateSessionSecret()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    private static StudentAssessmentDto ToStudentAssessment(Assessment assessment)
    {
        return new StudentAssessmentDto(
            assessment.Id,
            assessment.Title,
            assessment.Questions.OrderBy(question => question.OrderIndex).Select(question => new StudentQuestionDto(
                question.Id,
                question.Type,
                question.Prompt,
                question.Points,
                question.OrderIndex,
                question.Options.OrderBy(option => option.OrderIndex).Select(option => new StudentQuestionOptionDto(
                    option.Id,
                    option.Label,
                    option.OrderIndex)).ToArray())).ToArray());
    }

    private static decimal? TryScoreAutomatically(Question question, JsonElement answer)
    {
        if (question.RequiresManualScoring())
        {
            return null;
        }

        var selectedOptionIds = ExtractSelectedOptionIds(answer).ToHashSet();
        var correctOptionIds = question.Options
            .Where(option => option.IsCorrect)
            .Select(option => option.Id)
            .ToHashSet();

        return selectedOptionIds.SetEquals(correctOptionIds) ? question.Points : 0;
    }

    private static IEnumerable<Guid> ExtractSelectedOptionIds(JsonElement answer)
    {
        if (answer.ValueKind == JsonValueKind.Object)
        {
            if (answer.TryGetProperty("selectedOptionIds", out var selectedOptions) &&
                selectedOptions.ValueKind == JsonValueKind.Array)
            {
                foreach (var option in selectedOptions.EnumerateArray())
                {
                    if (Guid.TryParse(option.GetString(), out var optionId))
                    {
                        yield return optionId;
                    }
                }
            }
            else if (answer.TryGetProperty("optionId", out var option) &&
                Guid.TryParse(option.GetString(), out var optionId))
            {
                yield return optionId;
            }
        }
        else if (answer.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in answer.EnumerateArray())
            {
                if (Guid.TryParse(option.GetString(), out var optionId))
                {
                    yield return optionId;
                }
            }
        }
    }
}
