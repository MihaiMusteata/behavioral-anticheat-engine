using System.Text.Json;
using BehavioralAnticheatEngine.Domain.Behavior;

namespace BehavioralAnticheatEngine.Application.Behavior;

public abstract class BehavioralEventProcessorBase : IBehavioralEventProcessor
{
    public abstract string EventType { get; }

    public Task ProcessAsync(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate, CancellationToken cancellationToken = default)
    {
        aggregate.RegisterEvent(eventRecord.EventType, eventRecord.ReceivedAtUtc);
        if (HasValidationFlag(eventRecord.ValidationFlags, "seq_gap"))
        {
            aggregate.RegisterSequenceGapWarning();
        }

        RegisterSpecificFeatures(eventRecord, aggregate);
        return Task.CompletedTask;
    }

    protected virtual void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
    }

    protected static bool TryReadBoolean(BehavioralEventRecord eventRecord, string propertyName, out bool value)
    {
        value = false;

        try
        {
            using var document = JsonDocument.Parse(eventRecord.PayloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(propertyName, out var property) ||
                property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }

            value = property.GetBoolean();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasValidationFlag(string validationFlags, string expected)
    {
        return validationFlags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(expected, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class WindowBlurProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "window_blur";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterFocusLost();
    }
}

public sealed class WindowFocusProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "window_focus";
}

public sealed class VisibilityChangeProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "visibility_change";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        if (TryReadBoolean(eventRecord, "hidden", out var hidden) && hidden)
        {
            aggregate.RegisterVisibilityHidden();
        }
    }
}

public sealed class FullscreenEnterProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "fullscreen_enter";
}

public sealed class FullscreenExitProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "fullscreen_exit";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterFullscreenExit();
    }
}

public sealed class WindowResizeProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "window_resize";
}

public sealed class ClipboardCopyProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "copy";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterCopy();
    }
}

public sealed class ClipboardCutProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "cut";
}

public sealed class ClipboardPasteProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "paste";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterPaste();
    }
}

public sealed class TextBurstDetectedProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "text_burst_detected";
}

public sealed class RightClickProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "right_click";
}

public sealed class KeyboardShortcutProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "keyboard_shortcut";
}

public sealed class TypingSpeedSampleProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "typing_speed_sample";
}

public sealed class DevtoolsProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "devtools_heuristic_triggered";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterDevtoolsSignal();
    }
}

public sealed class PrintAttemptProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "print_attempt";
}

public sealed class QuestionViewStartProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "question_view_start";
}

public sealed class QuestionViewEndProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "question_view_end";
}

public sealed class QuestionNavigationProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "question_navigation";
}

public sealed class AnswerChangeProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "answer_change";
}

public sealed class InactivityProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "idle_detected";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterInactivitySignal();
    }
}

public sealed class SessionStartProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "session_start";
}

public sealed class SessionEndProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "session_end";
}

public sealed class SessionResumeProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "session_resume";
}

public sealed class MultipleTabsDetectedProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "multiple_tabs_detected";
}

public sealed class PageReloadProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "page_reload";
}

public sealed class PageUnloadProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "page_unload";
}

public sealed class EventSequenceGapProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "event_sequence_gap";
}

public sealed class EventValidationFailedProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "event_validation_failed";
}

public sealed class EventSilenceDetectedProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "event_silence_detected";
}

public sealed class LegacyWindowBlurProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "window.blur";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterFocusLost();
    }
}

public sealed class LegacyVisibilityHiddenProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "visibility.hidden";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterVisibilityHidden();
    }
}

public sealed class LegacyClipboardCopyProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "clipboard.copy";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterCopy();
    }
}

public sealed class LegacyClipboardPasteProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "clipboard.paste";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterPaste();
    }
}

public sealed class LegacyFullscreenExitProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "fullscreen.exit";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterFullscreenExit();
    }
}

public sealed class LegacyDevtoolsProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "devtools.suspected";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterDevtoolsSignal();
    }
}

public sealed class LegacyInactivityProcessor : BehavioralEventProcessorBase
{
    public override string EventType => "activity.inactive";

    protected override void RegisterSpecificFeatures(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate)
    {
        aggregate.RegisterInactivitySignal();
    }
}

public sealed class GenericBehavioralEventProcessor : BehavioralEventProcessorBase
{
    public const string FallbackEventType = "*";

    public override string EventType => FallbackEventType;
}
