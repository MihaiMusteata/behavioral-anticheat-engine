export type QuestionType = 'single_choice' | 'multiple_choice' | 'free_text'

export type QuestionOption = {
  id: string
  label: string
  isCorrect?: boolean
  orderIndex: number
}

export type Question = {
  id: string
  type: QuestionType
  prompt: string
  points: number
  orderIndex: number
  options: QuestionOption[]
}

export type Assessment = {
  id: string
  title: string
  description: string | null
  isPublished: boolean
  questions: Question[]
}

export type AssessmentSummary = Omit<Assessment, 'questions'> & {
  questionCount: number
}

export type ExamSchedule = {
  id: string
  assessmentId: string
  assessmentTitle: string
  name: string
  durationMinutes: number
  startsAtUtc: string | null
  endsAtUtc: string | null
  status: string
  accessPinCode: string
}

export type StudentAssessment = {
  id: string
  title: string
  questions: Question[]
}

export type ExistingAnswer = {
  questionId: string
  answerJson: string
}

export type StartExamSessionResponse = {
  sessionId: string
  examScheduleId: string
  assessmentId: string
  participantName: string
  endsAtUtc: string
  serverTimeUtc: string
  behaviorSigningSecret: string
  assessment: StudentAssessment
  existingAnswers: ExistingAnswer[]
}

export type GuestJoinResponse = {
  userId: string
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
  session: StartExamSessionResponse
}

export type SaveAnswerResponse = {
  questionId: string
  autoScore: number | null
  requiresManualScoring: boolean
}

export type SubmitExamResponse = {
  sessionId: string
  status: string
  submittedAtUtc: string
  score: number | null
}

export type StudentUser = {
  id: string
  email: string
  role: string
}
