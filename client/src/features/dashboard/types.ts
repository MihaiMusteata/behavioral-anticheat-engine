export type DetectionVote = {
  model: string
  probability: number | null
  vote: string
}

// Suspicion from the AI voting ensemble, computed after submit.
// status: waiting (not submitted) | processing (queued / scoring) | completed | failed
export type SessionDetection = {
  status: 'waiting' | 'processing' | 'completed' | 'failed'
  score: number | null
  decision: string | null
  level: string | null
  method: string | null
  votersUsed: number | null
  models: DetectionVote[]
  requestedAtUtc: string | null
  completedAtUtc: string | null
  error: string | null
}

export type QuestionDetection = {
  questionId: string
  questionIndex: number
  score: number | null
  decision: string | null
  level: string | null
  // majority vote: suspectVotes of votersUsed (2 of 3); score = mean probability
  suspectVotes: number | null
  votersUsed: number | null
  models: DetectionVote[]
  evidence: Record<string, number | null>
}

export type DashboardSessionRow = {
  sessionId: string
  studentId: string
  studentEmail: string
  status: string
  startedAtUtc: string
  lastActivityAtUtc: string
  detection: SessionDetection
  totalEvents: number
  liveStatus: string
}

export type ExamDashboard = {
  examScheduleId: string
  name: string
  sessions: DashboardSessionRow[]
  riskDistribution: {
    low: number
    medium: number
    high: number
    processing: number
    waiting: number
  }
  eventFrequencies: Array<{
    eventType: string
    count: number
  }>
}

export type TimelineEvent = {
  id: string
  seq: number
  timestampUtc: string
  receivedAtUtc?: string
  questionId: string | null
  eventType: string
  payloadJson: string
  validationFlags: string
}

export type SessionTimeline = {
  sessionId: string
  studentId: string
  studentEmail: string
  status: string
  detection: SessionDetection
  selfReportedCheated: boolean | null
  selectedQuestionId: string | null
  questions: Array<{
    questionId: string
    orderIndex: number
    label: string
    prompt: string
    detection: QuestionDetection | null
    options: Array<{ optionId: string; label: string }>
  }>
  answers: Array<{
    questionId: string
    prompt: string
    answerJson: string
    requiresManualScoring: boolean
    autoScore: number | null
    manualScore: number | null
    manualFeedback: string | null
  }>
  events: TimelineEvent[]
}

export type DashboardLiveEvent = {
  examScheduleId: string
  sessionId: string
  studentId: string
  type: string
  payloadJson: string
  occurredAtUtc: string
}
