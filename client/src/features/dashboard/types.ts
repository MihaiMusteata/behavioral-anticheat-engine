export type DashboardSessionRow = {
  sessionId: string
  studentId: string
  studentEmail: string
  status: string
  startedAtUtc: string
  lastActivityAtUtc: string
  riskScore: number
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
  riskScore: number
  selfReportedCheated: boolean | null
  selectedQuestionId: string | null
  selectedQuestionRiskScore: number | null
  questions: Array<{
    questionId: string
    orderIndex: number
    label: string
    prompt: string
    riskScore: number
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
  riskSnapshots: Array<{
    score: number
    reason: string
    createdAtUtc: string
  }>
}

export type DashboardLiveEvent = {
  examScheduleId: string
  sessionId: string
  studentId: string
  type: string
  payloadJson: string
  occurredAtUtc: string
}
