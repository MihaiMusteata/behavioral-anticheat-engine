import { forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState, type ReactNode } from 'react'
import { Icon } from '@iconify/react'
import QRCode from 'qrcode'
import { toast } from 'react-toastify'
import { api, getAccessToken, getErrorMessage, resolveApiUrl } from '../../lib/api'
import { Shell } from '../../shared/Shell'
import type { Assessment, AssessmentSummary, ExamSchedule, Question, QuestionType } from '../exams/types'
import type { DashboardLiveEvent, DashboardSessionRow, ExamDashboard, SessionTimeline, TimelineEvent } from './types'

type Tab = 'tests' | 'exams' | 'dashboard'
type QuestionForm = {
  id: string | null
  type: QuestionType
  prompt: string
  points: number
  orderIndex: number
  options: Array<{ label: string; isCorrect: boolean; orderIndex: number }>
}

const emptyQuestionForm: QuestionForm = {
  id: null,
  type: 'single_choice',
  prompt: '',
  points: 1,
  orderIndex: 1,
  options: [
    { label: '', isCorrect: true, orderIndex: 1 },
    { label: '', isCorrect: false, orderIndex: 2 },
  ],
}

export type DashboardPanelHandle = {
  focusExam: (examId: string) => void
}

export function AdminApp() {
  const [tab, setTab] = useState<Tab>('tests')
  const [assessments, setAssessments] = useState<AssessmentSummary[]>([])
  const [selectedAssessment, setSelectedAssessment] = useState<Assessment | null>(null)
  const [exams, setExams] = useState<ExamSchedule[]>([])
  const dashboardPanelRef = useRef<DashboardPanelHandle>(null)

  async function loadReferenceData() {
    try {
      const [assessmentResponse, examResponse] = await Promise.all([
        api.get<AssessmentSummary[]>('/api/assessments'),
        api.get<ExamSchedule[]>('/api/exams'),
      ])
      setAssessments(assessmentResponse.data)
      setExams(examResponse.data)
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  async function loadAssessment(assessmentId: string) {
    try {
      const response = await api.get<Assessment>(`/api/assessments/${assessmentId}`)
      setSelectedAssessment(response.data)
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  useEffect(() => {
    let ignore = false

    void fetchReferenceData()
      .then((data) => {
        if (ignore) {
          return
        }

        setAssessments(data.assessments)
        setExams(data.exams)
      })
      .catch((error: unknown) => {
        if (!ignore) {
          toast.error(getErrorMessage(error))
        }
      })

    return () => {
      ignore = true
    }
  }, [])

  return (
    <Shell title="Proctor dashboard">
      <main className="mx-auto max-w-7xl px-4 py-6">
        <nav className="mb-5 flex flex-wrap gap-2">
          {[
            ['tests', 'Tests', 'mdi:file-document-edit'],
            ['exams', 'Exam schedules', 'mdi:calendar-clock'],
            ['dashboard', 'Live dashboard', 'mdi:view-dashboard'],
          ].map(([id, label, icon]) => (
            <button
              key={id}
              type="button"
              onClick={() => setTab(id as Tab)}
              className={`inline-flex items-center gap-2 rounded-md px-3 py-2 text-sm font-semibold ${
                tab === id ? 'bg-slate-950 text-white' : 'border border-slate-300 bg-white text-slate-700 hover:bg-slate-50'
              }`}
            >
              <Icon icon={icon} className="h-4 w-4" />
              {label}
            </button>
          ))}
        </nav>

        {tab === 'tests' ? (
          <TestsPanel
            key={selectedAssessment?.id ?? 'new-assessment'}
            assessments={assessments}
            selectedAssessment={selectedAssessment}
            onReload={() => void loadReferenceData()}
            onLoadAssessment={(assessmentId) => void loadAssessment(assessmentId)}
            onSelectedAssessmentChange={setSelectedAssessment}
          />
        ) : null}

        {tab === 'exams' ? (
          <ExamSchedulesPanel
            assessments={assessments}
            exams={exams}
            onReload={() => void loadReferenceData()}
            onExamStarted={(examId) => {
              setTab('dashboard')
              dashboardPanelRef.current?.focusExam(examId)
            }}
          />
        ) : null}

        <div className={tab === 'dashboard' ? '' : 'hidden'}>
          <DashboardPanel ref={dashboardPanelRef} exams={exams} active={tab === 'dashboard'} />
        </div>
      </main>
    </Shell>
  )
}

function TestsPanel({
  assessments,
  selectedAssessment,
  onReload,
  onLoadAssessment,
  onSelectedAssessmentChange,
}: {
  assessments: AssessmentSummary[]
  selectedAssessment: Assessment | null
  onReload: () => void
  onLoadAssessment: (assessmentId: string) => void
  onSelectedAssessmentChange: (assessment: Assessment | null) => void
}) {
  const [title, setTitle] = useState(selectedAssessment?.title ?? '')
  const [description, setDescription] = useState(selectedAssessment?.description ?? '')
  const [publish, setPublish] = useState(selectedAssessment?.isPublished ?? true)
  const [questionForm, setQuestionForm] = useState<QuestionForm>(emptyQuestionForm)

  async function saveAssessment() {
    try {
      const payload = { title, description, publish }
      if (selectedAssessment) {
        const response = await api.put<Assessment>(`/api/assessments/${selectedAssessment.id}`, payload)
        onSelectedAssessmentChange(response.data)
      } else {
        const response = await api.post<Assessment>('/api/assessments', payload)
        onSelectedAssessmentChange(response.data)
      }
      toast.success('Test saved.')
      onReload()
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  async function deleteAssessment(assessmentId: string) {
    try {
      await api.delete(`/api/assessments/${assessmentId}`)
      if (selectedAssessment?.id === assessmentId) {
        onSelectedAssessmentChange(null)
      }
      toast.success('Test deleted.')
      onReload()
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  async function saveQuestion() {
    if (!selectedAssessment) {
      toast.warn('Select a test first.')
      return
    }

    const payload = {
      type: questionForm.type,
      prompt: questionForm.prompt,
      points: questionForm.points,
      orderIndex: questionForm.orderIndex,
      options: questionForm.type === 'free_text' ? [] : questionForm.options,
    }

    try {
      if (questionForm.id) {
        await api.put(`/api/questions/${questionForm.id}`, payload)
      } else {
        await api.post(`/api/assessments/${selectedAssessment.id}/questions`, payload)
      }
      toast.success('Question saved.')
      setQuestionForm(emptyQuestionForm)
      onLoadAssessment(selectedAssessment.id)
      onReload()
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  async function deleteQuestion(questionId: string) {
    if (!selectedAssessment) {
      return
    }

    try {
      await api.delete(`/api/questions/${questionId}`)
      toast.success('Question deleted.')
      onLoadAssessment(selectedAssessment.id)
      onReload()
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  return (
    <section className="grid gap-4 lg:grid-cols-[320px_1fr]">
      <aside className="rounded-lg border border-slate-300 bg-white p-4">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="font-semibold">Tests</h2>
          <button
            type="button"
            onClick={() => {
              onSelectedAssessmentChange(null)
              setTitle('')
              setDescription('')
              setPublish(true)
              setQuestionForm(emptyQuestionForm)
            }}
            className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium hover:bg-slate-50"
          >
            New
          </button>
        </div>
        <div className="space-y-2">
          {assessments.map((assessment) => (
            <div key={assessment.id} className="rounded-md border border-slate-200 p-3">
              <button type="button" onClick={() => onLoadAssessment(assessment.id)} className="block text-left font-medium text-slate-950">
                {assessment.title}
              </button>
              <p className="text-xs text-slate-500">{assessment.questionCount} questions</p>
              <button
                type="button"
                onClick={() => void deleteAssessment(assessment.id)}
                className="mt-2 inline-flex items-center gap-1 text-xs font-medium text-rose-700"
              >
                <Icon icon="mdi:trash-can-outline" className="h-4 w-4" />
                Delete
              </button>
            </div>
          ))}
        </div>
      </aside>

      <div className="grid gap-4">
        <section className="rounded-lg border border-slate-300 bg-white p-4">
          <h2 className="mb-4 font-semibold">{selectedAssessment ? 'Edit test' : 'Create test'}</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <label className="md:col-span-2 text-sm font-medium text-slate-700">
              Title
              <input className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" value={title} onChange={(event) => setTitle(event.target.value)} />
            </label>
            <label className="md:col-span-2 text-sm font-medium text-slate-700">
              Description
              <textarea className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" value={description} onChange={(event) => setDescription(event.target.value)} />
            </label>
            <label className="inline-flex items-center gap-2 text-sm font-medium text-slate-700">
              <input type="checkbox" checked={publish} onChange={(event) => setPublish(event.target.checked)} />
              Published
            </label>
          </div>
          <button type="button" onClick={() => void saveAssessment()} className="mt-4 inline-flex items-center gap-2 rounded-md bg-emerald-700 px-3 py-2 text-sm font-semibold text-white">
            <Icon icon="mdi:content-save" className="h-4 w-4" />
            Save test
          </button>
        </section>

        {selectedAssessment ? (
          <section className="rounded-lg border border-slate-300 bg-white p-4">
            <div className="mb-4 flex items-center justify-between gap-3">
              <h2 className="font-semibold">Questions</h2>
              <button type="button" onClick={() => setQuestionForm(emptyQuestionForm)} className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium">
                New question
              </button>
            </div>

            <div className="mb-5 space-y-2">
              {selectedAssessment.questions.map((question) => (
                <div key={question.id} className="flex items-start justify-between gap-3 rounded-md border border-slate-200 p-3">
                  <button
                    type="button"
                    onClick={() => setQuestionForm({
                      id: question.id,
                      type: question.type,
                      prompt: question.prompt,
                      points: question.points,
                      orderIndex: question.orderIndex,
                      options: question.options.map((option) => ({
                        label: option.label,
                        isCorrect: Boolean(option.isCorrect),
                        orderIndex: option.orderIndex,
                      })),
                    })}
                    className="text-left"
                  >
                    <p className="font-medium">{question.prompt}</p>
                    <p className="text-xs text-slate-500">{question.type} · {question.points} pts</p>
                  </button>
                  <button type="button" onClick={() => void deleteQuestion(question.id)} className="text-rose-700">
                    <Icon icon="mdi:trash-can-outline" className="h-5 w-5" />
                  </button>
                </div>
              ))}
            </div>

            <QuestionEditor form={questionForm} onChange={setQuestionForm} onSave={() => void saveQuestion()} />
          </section>
        ) : null}
      </div>
    </section>
  )
}

function QuestionEditor({ form, onChange, onSave }: { form: QuestionForm; onChange: (form: QuestionForm) => void; onSave: () => void }) {
  return (
    <div className="rounded-md bg-slate-50 p-3">
      <div className="grid gap-3 md:grid-cols-[180px_1fr_120px_120px]">
        <label className="text-sm font-medium text-slate-700">
          Type
          <select className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" value={form.type} onChange={(event) => onChange({ ...form, type: event.target.value as QuestionType })}>
            <option value="single_choice">Single choice</option>
            <option value="multiple_choice">Multiple choice</option>
            <option value="free_text">Free text</option>
          </select>
        </label>
        <label className="text-sm font-medium text-slate-700">
          Prompt
          <input className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" value={form.prompt} onChange={(event) => onChange({ ...form, prompt: event.target.value })} />
        </label>
        <label className="text-sm font-medium text-slate-700">
          Points
          <input className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" type="number" min={0.5} step={0.5} value={form.points} onChange={(event) => onChange({ ...form, points: Number(event.target.value) })} />
        </label>
        <label className="text-sm font-medium text-slate-700">
          Order
          <input className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" type="number" min={1} value={form.orderIndex} onChange={(event) => onChange({ ...form, orderIndex: Number(event.target.value) })} />
        </label>
      </div>

      {form.type !== 'free_text' ? (
        <div className="mt-3 space-y-2">
          {form.options.map((option, index) => (
            <div key={index} className="grid gap-2 md:grid-cols-[1fr_120px_40px]">
              <input
                className="rounded-md border border-slate-300 px-3 py-2"
                value={option.label}
                onChange={(event) => onChange({
                  ...form,
                  options: form.options.map((current, optionIndex) => optionIndex === index ? { ...current, label: event.target.value } : current),
                })}
              />
              <label className="inline-flex items-center gap-2 text-sm text-slate-700">
                <input
                  type="checkbox"
                  checked={option.isCorrect}
                  onChange={(event) => onChange({
                    ...form,
                    options: form.options.map((current, optionIndex) => optionIndex === index ? { ...current, isCorrect: event.target.checked } : current),
                  })}
                />
                Correct
              </label>
              <button
                type="button"
                onClick={() => onChange({ ...form, options: form.options.filter((_, optionIndex) => optionIndex !== index) })}
                className="grid place-items-center rounded-md border border-slate-300"
              >
                <Icon icon="mdi:close" className="h-4 w-4" />
              </button>
            </div>
          ))}
          <button
            type="button"
            onClick={() => onChange({ ...form, options: [...form.options, { label: '', isCorrect: false, orderIndex: form.options.length + 1 }] })}
            className="inline-flex items-center gap-2 rounded-md border border-slate-300 px-3 py-2 text-sm font-medium"
          >
            <Icon icon="mdi:plus" className="h-4 w-4" />
            Add option
          </button>
        </div>
      ) : null}

      <button type="button" onClick={onSave} className="mt-4 inline-flex items-center gap-2 rounded-md bg-slate-950 px-3 py-2 text-sm font-semibold text-white">
        <Icon icon="mdi:content-save" className="h-4 w-4" />
        Save question
      </button>
    </div>
  )
}

function ExamSchedulesPanel({
  assessments,
  exams,
  onReload,
  onExamStarted,
}: {
  assessments: AssessmentSummary[]
  exams: ExamSchedule[]
  onReload: () => void
  onExamStarted: (examId: string) => void
}) {
  const [editingExamId, setEditingExamId] = useState<string | null>(null)
  const [assessmentId, setAssessmentId] = useState('')
  const [name, setName] = useState('')
  const [durationMinutes, setDurationMinutes] = useState(45)
  const [starting, setStarting] = useState(false)
  const activeAssessmentId = assessmentId || assessments[0]?.id || ''

  function resetForm() {
    setEditingExamId(null)
    setAssessmentId('')
    setName('')
    setDurationMinutes(45)
  }

  function editExam(exam: ExamSchedule) {
    setEditingExamId(exam.id)
    setAssessmentId(exam.assessmentId)
    setName(exam.name)
    setDurationMinutes(exam.durationMinutes)
  }

  async function saveExam() {
    try {
      if (editingExamId) {
        await api.put(`/api/exams/${editingExamId}`, { name, durationMinutes })
      } else {
        await api.post('/api/exams', { assessmentId: activeAssessmentId, name, durationMinutes })
      }
      toast.success('Exam schedule saved.')
      resetForm()
      onReload()
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  async function startNow() {
    setStarting(true)
    try {
      let examId = editingExamId
      if (examId) {
        await api.put(`/api/exams/${examId}`, { name, durationMinutes })
      } else {
        const response = await api.post<ExamSchedule>('/api/exams', { assessmentId: activeAssessmentId, name, durationMinutes })
        examId = response.data.id
      }

      await api.post(`/api/exams/${examId}/start`)
      toast.success('Exam started — opening live dashboard.')
      resetForm()
      onReload()
      onExamStarted(examId)
    } catch (error) {
      toast.error(getErrorMessage(error))
    } finally {
      setStarting(false)
    }
  }

  async function mutateExam(examId: string, action: 'start' | 'stop' | 'delete') {
    try {
      if (action === 'delete') {
        await api.delete(`/api/exams/${examId}`)
      } else {
        await api.post(`/api/exams/${examId}/${action}`)
      }
      onReload()
      if (action === 'start') {
        toast.success('Exam started — opening live dashboard.')
        onExamStarted(examId)
      }
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  return (
    <section className="grid gap-4 lg:grid-cols-[380px_1fr]">
      <form className="rounded-lg border border-slate-300 bg-white p-4" onSubmit={(event) => { event.preventDefault(); void saveExam() }}>
        <h2 className="mb-4 font-semibold">{editingExamId ? 'Edit schedule' : 'Create schedule'}</h2>
        <div className="space-y-3">
          <label className="block text-sm font-medium text-slate-700">
            Test
            <select className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" value={activeAssessmentId} onChange={(event) => setAssessmentId(event.target.value)} disabled={Boolean(editingExamId)}>
              {assessments.map((assessment) => (
                <option key={assessment.id} value={assessment.id}>{assessment.title}</option>
              ))}
            </select>
          </label>
          <label className="block text-sm font-medium text-slate-700">
            Name
            <input className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" value={name} onChange={(event) => setName(event.target.value)} />
          </label>
          <label className="block text-sm font-medium text-slate-700">
            Duration (minutes)
            <input
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2"
              type="number"
              min={1}
              value={durationMinutes}
              onChange={(event) => setDurationMinutes(Number(event.target.value))}
            />
          </label>
          <p className="text-xs text-slate-500">
            Participation is open to anyone with the access code — no student roster needed. Starts/ends are set automatically from this
            duration the moment you start the exam, and the PIN can be shared from the live dashboard.
          </p>
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          <button type="submit" className="inline-flex items-center gap-2 rounded-md border border-slate-300 px-3 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50">
            <Icon icon="mdi:content-save" className="h-4 w-4" />
            Save schedule
          </button>
          <button
            type="button"
            onClick={() => void startNow()}
            disabled={starting}
            className="inline-flex items-center gap-2 rounded-md bg-emerald-700 px-3 py-2 text-sm font-semibold text-white disabled:opacity-60"
          >
            <Icon icon={starting ? 'mdi:loading' : 'mdi:play'} className={`h-4 w-4 ${starting ? 'animate-spin' : ''}`} />
            Start now
          </button>
        </div>
      </form>

      <section className="rounded-lg border border-slate-300 bg-white p-4">
        <h2 className="mb-4 font-semibold">Schedules</h2>
        <div className="space-y-2">
          {exams.map((exam) => (
            <div key={exam.id} className="rounded-md border border-slate-200 p-3">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="font-medium">{exam.name}</p>
                  <p className="text-sm text-slate-600">
                    {exam.assessmentTitle} · {exam.durationMinutes} min
                    {exam.startsAtUtc && exam.endsAtUtc ? ` · ${formatDate(exam.startsAtUtc)} - ${formatDate(exam.endsAtUtc)}` : ' · not started yet'}
                  </p>
                </div>
                <span className="rounded bg-slate-100 px-2 py-1 text-xs font-medium text-slate-700">{exam.status}</span>
              </div>
              <div className="mt-3 flex flex-wrap items-center gap-2">
                <button type="button" onClick={() => editExam(exam)} className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium">Edit</button>
                <button type="button" onClick={() => void mutateExam(exam.id, 'start')} className="rounded-md border border-emerald-300 px-2 py-1 text-xs font-medium text-emerald-800">Start now</button>
                <button type="button" onClick={() => void mutateExam(exam.id, 'stop')} className="rounded-md border border-amber-300 px-2 py-1 text-xs font-medium text-amber-800">Stop</button>
                <button type="button" onClick={() => void mutateExam(exam.id, 'delete')} className="rounded-md border border-rose-300 px-2 py-1 text-xs font-medium text-rose-800">Delete</button>
              </div>
            </div>
          ))}
        </div>
      </section>
    </section>
  )
}

function ShareExam({ pinCode }: { pinCode: string }) {
  const joinUrl = useMemo(() => {
    if (!pinCode) {
      return ''
    }
    const url = new URL(window.location.origin + window.location.pathname)
    url.searchParams.set('pin', pinCode)
    return url.toString()
  }, [pinCode])
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null)

  useEffect(() => {
    if (!joinUrl) {
      setQrDataUrl(null)
      return undefined
    }

    let ignore = false
    void QRCode.toDataURL(joinUrl, { width: 180, margin: 1 })
      .then((dataUrl) => {
        if (!ignore) {
          setQrDataUrl(dataUrl)
        }
      })
      .catch(() => {
        if (!ignore) {
          setQrDataUrl(null)
        }
      })
    return () => {
      ignore = true
    }
  }, [joinUrl])

  async function copyLink() {
    try {
      await navigator.clipboard.writeText(joinUrl)
      toast.success('Join link copied.')
    } catch {
      toast.error('Could not copy the link.')
    }
  }

  if (!pinCode) {
    return (
      <div className="flex items-center gap-3 rounded-lg border border-dashed border-slate-300 bg-slate-50 p-4 text-sm text-slate-600">
        <Icon icon="mdi:qrcode" className="h-6 w-6 shrink-0 text-slate-400" />
        Start this exam to generate an access code, join link and QR code.
      </div>
    )
  }

  return (
    <div className="flex flex-wrap items-center gap-4 rounded-lg border border-dashed border-emerald-300 bg-emerald-50 p-4">
      {qrDataUrl ? <img src={qrDataUrl} alt={`QR code to join with PIN ${pinCode}`} className="h-24 w-24 rounded bg-white p-1" /> : null}
      <div className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-wide text-emerald-800">Access PIN</p>
        <p className="text-2xl font-bold tracking-widest text-emerald-900">{pinCode}</p>
        <button
          type="button"
          onClick={() => void copyLink()}
          className="inline-flex items-center gap-1 rounded-md border border-emerald-700 px-2 py-1 text-xs font-semibold text-emerald-800 hover:bg-white"
        >
          <Icon icon="mdi:content-copy" className="h-4 w-4" />
          Copy join link
        </button>
      </div>
    </div>
  )
}

const DashboardPanel = forwardRef<DashboardPanelHandle, { exams: ExamSchedule[]; active: boolean }>(function DashboardPanel({ exams, active }, ref) {
  const [examId, setExamId] = useState('')
  const [dashboard, setDashboard] = useState<ExamDashboard | null>(null)
  const [dashboardRefreshVersion, setDashboardRefreshVersion] = useState(0)
  const [selectedSessionId, setSelectedSessionId] = useState<string | null>(null)
  const [selectedSession, setSelectedSession] = useState<SessionTimeline | null>(null)
  const [timelineRefreshVersion, setTimelineRefreshVersion] = useState(0)
  const [examQuestions, setExamQuestions] = useState<Question[]>([])
  const dashboardRefreshTimerRef = useRef<number | null>(null)
  const dashboardExamIdRef = useRef('')
  const dashboardSessionIdsRef = useRef(new Set<string>())
  const selectedSessionIdRef = useRef<string | null>(null)
  const timelineRefreshTimerRef = useRef<number | null>(null)
  const timelineRefreshQueuedRef = useRef(false)
  const timelineRequestSequenceRef = useRef(0)
  const activeTimelineRequestRef = useRef<{ id: number; sessionId: string } | null>(null)

  const activeExamId = useMemo(
    () => exams.some((exam) => exam.id === examId) ? examId : exams[0]?.id ?? '',
    [examId, exams],
  )
  const selectedExam = useMemo(() => exams.find((exam) => exam.id === activeExamId), [activeExamId, exams])

  useEffect(() => {
    if (!active || !selectedExam) {
      return undefined
    }

    const abortController = new AbortController()
    void fetchAssessment(selectedExam.assessmentId, abortController.signal)
      .then((assessment) => setExamQuestions(assessment.questions))
      .catch((error: unknown) => {
        if (!abortController.signal.aborted) {
          toast.error(getErrorMessage(error))
        }
      })

    return () => abortController.abort()
  }, [active, selectedExam])

  const queueDashboardRefresh = useCallback(() => {
    if (dashboardRefreshTimerRef.current !== null) {
      return
    }

    dashboardRefreshTimerRef.current = window.setTimeout(() => {
      dashboardRefreshTimerRef.current = null
      setDashboardRefreshVersion((current) => current + 1)
    }, 200)
  }, [])

  const queueTimelineRefresh = useCallback((sessionId: string) => {
    if (selectedSessionIdRef.current !== sessionId) {
      return
    }

    if (activeTimelineRequestRef.current?.sessionId === sessionId) {
      timelineRefreshQueuedRef.current = true
      return
    }

    if (timelineRefreshTimerRef.current !== null) {
      return
    }

    timelineRefreshTimerRef.current = window.setTimeout(() => {
      timelineRefreshTimerRef.current = null
      if (activeTimelineRequestRef.current?.sessionId === sessionId) {
        timelineRefreshQueuedRef.current = true
        return
      }

      if (selectedSessionIdRef.current === sessionId) {
        setTimelineRefreshVersion((current) => current + 1)
      }
    }, 200)
  }, [])

  useEffect(() => () => {
    if (dashboardRefreshTimerRef.current !== null) {
      window.clearTimeout(dashboardRefreshTimerRef.current)
    }

    if (timelineRefreshTimerRef.current !== null) {
      window.clearTimeout(timelineRefreshTimerRef.current)
    }

    timelineRefreshQueuedRef.current = false
  }, [])

  useEffect(() => {
    if (!active || !activeExamId) {
      return undefined
    }

    const abortController = new AbortController()
    let ignore = false

    void fetchExamDashboard(activeExamId, abortController.signal)
      .then((incoming) => {
        if (ignore) {
          return
        }

        if (dashboardExamIdRef.current !== activeExamId) {
          dashboardExamIdRef.current = activeExamId
          dashboardSessionIdsRef.current.clear()
        }
        incoming.sessions.forEach((session) => dashboardSessionIdsRef.current.add(session.sessionId))
        setDashboard((current) => mergeDashboardSnapshot(current, incoming))
      })
      .catch((error: unknown) => {
        if (!abortController.signal.aborted) {
          toast.error(getErrorMessage(error))
        }
      })

    return () => {
      ignore = true
      abortController.abort()
    }
  }, [active, activeExamId, dashboardRefreshVersion])

  useEffect(() => {
    if (!active || !selectedSessionId) {
      return undefined
    }

    const abortController = new AbortController()
    const requestId = timelineRequestSequenceRef.current + 1
    timelineRequestSequenceRef.current = requestId
    activeTimelineRequestRef.current = { id: requestId, sessionId: selectedSessionId }
    let ignore = false

    void fetchSessionTimeline(selectedSessionId, abortController.signal)
      .then((timeline) => {
        if (!ignore && selectedSessionIdRef.current === selectedSessionId) {
          setSelectedSession(sortSessionTimeline(timeline))
        }
      })
      .catch((error: unknown) => {
        if (!abortController.signal.aborted) {
          toast.error(getErrorMessage(error))
        }
      })
      .finally(() => {
        if (activeTimelineRequestRef.current?.id !== requestId) {
          return
        }

        activeTimelineRequestRef.current = null
        if (timelineRefreshQueuedRef.current && selectedSessionIdRef.current === selectedSessionId) {
          timelineRefreshQueuedRef.current = false
          setTimelineRefreshVersion((current) => current + 1)
        }
      })

    return () => {
      ignore = true
      abortController.abort()
    }
  }, [active, selectedSessionId, timelineRefreshVersion])

  useEffect(() => {
    if (!active || !activeExamId) {
      return undefined
    }

    const abortController = new AbortController()
    void streamDashboard(activeExamId, abortController.signal, (event) => {
      if (event.examScheduleId !== activeExamId) {
        return
      }

      const payload = parsePayload(event.payloadJson)
      const liveState = getLiveState(payload)
      const isKnownSession = dashboardSessionIdsRef.current.has(event.sessionId)
      if (liveState) {
        dashboardSessionIdsRef.current.add(event.sessionId)
        setDashboard((current) => applyDashboardLiveEvent(current, event, payload, liveState))
      }

      if (!liveState || !isKnownSession) {
        queueDashboardRefresh()
      }

      if (selectedSessionIdRef.current === event.sessionId) {
        queueTimelineRefresh(event.sessionId)
      }
    })

    return () => {
      abortController.abort()
    }
  }, [active, activeExamId, queueDashboardRefresh, queueTimelineRefresh])

  function changeExam(nextExamId: string) {
    dashboardExamIdRef.current = nextExamId
    dashboardSessionIdsRef.current.clear()
    selectedSessionIdRef.current = null
    timelineRefreshQueuedRef.current = false
    setExamId(nextExamId)
    setDashboard(null)
    setExamQuestions([])
    setSelectedSessionId(null)
    setSelectedSession(null)
  }

  // Exposes an imperative entry point so a sibling panel's "Start" click
  // (a real event handler, in a different component) can jump the
  // dashboard to that exam - not an effect reacting to a prop, so there's
  // no synchronous setState-in-effect and no ref access during render.
  useImperativeHandle(ref, () => ({
    focusExam: changeExam,
  }))

  function selectSession(sessionId: string) {
    if (selectedSessionIdRef.current === sessionId) {
      queueTimelineRefresh(sessionId)
      return
    }

    if (timelineRefreshTimerRef.current !== null) {
      window.clearTimeout(timelineRefreshTimerRef.current)
      timelineRefreshTimerRef.current = null
    }
    timelineRefreshQueuedRef.current = false
    selectedSessionIdRef.current = sessionId
    setSelectedSessionId(sessionId)
    setSelectedSession(null)
  }

  function refreshDashboardNow() {
    if (dashboardRefreshTimerRef.current !== null) {
      window.clearTimeout(dashboardRefreshTimerRef.current)
      dashboardRefreshTimerRef.current = null
    }
    setDashboardRefreshVersion((current) => current + 1)
  }

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-300 bg-white p-4">
        <label className="text-sm font-medium text-slate-700">
          Exam
          <select className="ml-2 rounded-md border border-slate-300 px-3 py-2" value={activeExamId} onChange={(event) => changeExam(event.target.value)}>
            {exams.map((exam) => (
              <option key={exam.id} value={exam.id}>{exam.name}</option>
            ))}
          </select>
        </label>
        <button type="button" onClick={refreshDashboardNow} className="inline-flex items-center gap-2 rounded-md border border-slate-300 px-3 py-2 text-sm font-semibold">
          <Icon icon="mdi:refresh" className="h-4 w-4" />
          Refresh
        </button>
      </div>

      {selectedExam ? <ShareExam pinCode={selectedExam.accessPinCode} /> : null}

      {dashboard ? (
        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-3">
            <Metric label="Low risk" value={dashboard.riskDistribution.low} tone="emerald" />
            <Metric label="Medium risk" value={dashboard.riskDistribution.medium} tone="amber" />
            <Metric label="High risk" value={dashboard.riskDistribution.high} tone="rose" />
          </div>

          <section className="rounded-lg border border-slate-300 bg-white">
            <div className="border-b border-slate-200 p-4">
              <h2 className="font-semibold">{selectedExam?.name ?? dashboard.name}</h2>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                  <tr>
                    <th className="px-4 py-3">Student</th>
                    <th className="px-4 py-3">Status</th>
                    <th className="px-4 py-3">Suspicion</th>
                    <th className="px-4 py-3">Events</th>
                    <th className="px-4 py-3">Last activity</th>
                  </tr>
                </thead>
                <tbody>
                  {dashboard.sessions.map((session) => (
                    <tr key={session.sessionId} className="border-t border-slate-100 hover:bg-slate-50">
                      <td className="px-4 py-3">
                        <button type="button" onClick={() => selectSession(session.sessionId)} className="font-medium text-emerald-800">
                          {session.studentEmail}
                        </button>
                      </td>
                      <td className="px-4 py-3">{session.liveStatus}</td>
                      <td className="px-4 py-3">
                        <RiskBar score={session.riskScore} />
                      </td>
                      <td className="px-4 py-3">{session.totalEvents}</td>
                      <td className="px-4 py-3">{formatDate(session.lastActivityAtUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <CollapsibleSection title="Event frequency">
            <div className="space-y-2">
              {dashboard.eventFrequencies.map((event) => (
                <div key={event.eventType} className="flex items-center gap-3">
                  <span className="w-44 text-sm text-slate-700">{event.eventType}</span>
                  <div className="h-2 flex-1 rounded bg-slate-100">
                    <div className="h-2 rounded bg-emerald-600" style={{ width: `${Math.min(event.count * 12, 100)}%` }} />
                  </div>
                  <span className="w-8 text-right text-sm font-medium">{event.count}</span>
                </div>
              ))}
            </div>
          </CollapsibleSection>

          <CollapsibleSection title={`Exam items (${examQuestions.length})`}>
            <div className="space-y-2">
              {examQuestions.length === 0 ? (
                <p className="text-sm text-slate-500">No questions in this test.</p>
              ) : (
                examQuestions
                  .slice()
                  .sort((left, right) => left.orderIndex - right.orderIndex)
                  .map((question) => (
                    <div key={question.id} className="rounded-md border border-slate-200 p-3">
                      <div className="flex items-start justify-between gap-3">
                        <p className="font-medium">{question.prompt}</p>
                        <span className="shrink-0 text-xs text-slate-500">{question.points} pts</span>
                      </div>
                      <p className="mt-1 text-xs uppercase tracking-wide text-slate-500">{question.type.replaceAll('_', ' ')}</p>
                      {question.options.length > 0 ? (
                        <ul className="mt-2 space-y-1 text-sm text-slate-700">
                          {question.options.map((option) => (
                            <li key={option.id} className="flex items-center gap-2">
                              <Icon
                                icon={option.isCorrect ? 'mdi:check-circle' : 'mdi:circle-outline'}
                                className={option.isCorrect ? 'h-4 w-4 shrink-0 text-emerald-600' : 'h-4 w-4 shrink-0 text-slate-300'}
                              />
                              {option.label}
                            </li>
                          ))}
                        </ul>
                      ) : null}
                    </div>
                  ))
              )}
            </div>
          </CollapsibleSection>

          {selectedSession ? (
            <SessionDetail
              key={selectedSessionId ?? 'no-session'}
              timeline={selectedSession}
              onScored={() => {
                if (selectedSessionIdRef.current) {
                  queueTimelineRefresh(selectedSessionIdRef.current)
                }
              }}
            />
          ) : (
            <div className="rounded-lg border border-slate-300 bg-white p-6 text-slate-600">Select a student to inspect the timeline.</div>
          )}
        </div>
      ) : (
        <div className="rounded-lg border border-slate-300 bg-white p-6 text-slate-600">Select an exam to inspect live activity.</div>
      )}
    </section>
  )
})

function SessionDetail({ timeline, onScored }: { timeline: SessionTimeline; onScored: () => void }) {
  const [manualScores, setManualScores] = useState<Record<string, string>>({})
  const [auditScope, setAuditScope] = useState('all')
  const [selectedEventTypes, setSelectedEventTypes] = useState<string[]>([])
  const chronologicalEvents = useMemo(() => sortTimelineEvents(timeline.events), [timeline.events])

  const activeTimeline = timeline
  const selectedQuestion = auditScope === 'all' ? null : activeTimeline.questions.find((question) => question.questionId === auditScope) ?? null
  const scopedEvents = selectedQuestion
    ? filterTimelineEventsForQuestion(chronologicalEvents, selectedQuestion.questionId)
    : chronologicalEvents
  const eventTypes = Array.from(new Set(scopedEvents.map((event) => event.eventType))).sort((left, right) => left.localeCompare(right))
  const visibleEvents = selectedEventTypes.length > 0
    ? scopedEvents.filter((event) => selectedEventTypes.includes(event.eventType))
    : scopedEvents
  const visibleAnswers = selectedQuestion
    ? activeTimeline.answers.filter((answer) => answer.questionId === selectedQuestion.questionId)
    : activeTimeline.answers
  const activeRiskScore = selectedQuestion ? selectedQuestion.riskScore : activeTimeline.riskScore

  async function scoreAnswer(questionId: string) {
    const score = Number(manualScores[questionId] ?? '')
    if (Number.isNaN(score)) {
      toast.warn('Enter a numeric score.')
      return
    }

    try {
      await api.put(`/api/dashboard/sessions/${activeTimeline.sessionId}/answers/${questionId}/score`, { score, feedback: null })
      toast.success('Manual score saved.')
      onScored()
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  async function exportEvents() {
    try {
      const blob = new Blob([createEventsCsv(visibleEvents)], { type: 'text/csv;charset=utf-8' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `session-${activeTimeline.sessionId}-${selectedQuestion?.label ?? 'all'}-events.csv`
      document.body.appendChild(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
    } catch (error) {
      toast.error(getErrorMessage(error))
    }
  }

  return (
    <section className="space-y-4">
      <section className="rounded-lg border border-slate-300 bg-white p-4">
        <h2 className="font-semibold">{activeTimeline.studentEmail}</h2>
        <p className="mb-3 text-sm text-slate-600">{activeTimeline.status}</p>
        <RiskBar score={activeRiskScore} />
        <SelfReportBadge value={activeTimeline.selfReportedCheated} />
      </section>

      <section className="rounded-lg border border-slate-300 bg-white p-4">
        <h3 className="mb-3 font-semibold">Manual scoring</h3>
        <div className="space-y-3">
          {visibleAnswers.map((answer) => (
            <div key={answer.questionId} className="rounded-md border border-slate-200 p-3">
              <p className="text-sm font-medium">{answer.prompt}</p>
              <pre className="mt-2 max-h-28 overflow-auto rounded bg-slate-50 p-2 text-xs text-slate-700">{answer.answerJson}</pre>
              {answer.requiresManualScoring ? (
                <div className="mt-2 flex gap-2">
                  <input
                    className="w-24 rounded-md border border-slate-300 px-2 py-1 text-sm"
                    value={manualScores[answer.questionId] ?? answer.manualScore ?? ''}
                    onChange={(event) => setManualScores((current) => ({ ...current, [answer.questionId]: event.target.value }))}
                  />
                  <button type="button" onClick={() => void scoreAnswer(answer.questionId)} className="rounded-md bg-slate-950 px-3 py-1 text-sm font-semibold text-white">
                    Save
                  </button>
                </div>
              ) : (
                <p className="mt-2 text-sm text-slate-600">Auto score: {answer.autoScore ?? 0}</p>
              )}
            </div>
          ))}
        </div>
      </section>

      <section className="rounded-lg border border-slate-300 bg-white p-4">
        <div className="mb-3 space-y-3">
          <label className="block text-sm font-medium text-slate-700">
            Audit scope
            <select
              className="mt-1 w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              value={auditScope}
              onChange={(event) => {
                setAuditScope(event.target.value)
                setSelectedEventTypes([])
              }}
            >
              <option value="all">Tot auditul sesiunii</option>
              {activeTimeline.questions.map((question) => (
                <option key={question.questionId} value={question.questionId}>{question.label}</option>
              ))}
            </select>
          </label>

          {selectedQuestion ? (
            <div className="rounded-md bg-slate-50 p-3">
              <p className="text-xs font-semibold uppercase text-slate-500">{selectedQuestion.label}</p>
              <p className="mt-1 text-sm text-slate-700">{selectedQuestion.prompt}</p>
            </div>
          ) : null}
        </div>

        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 className="font-semibold">Activity timeline</h3>
            <p className="text-xs text-slate-500">{visibleEvents.length} / {scopedEvents.length} events</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => void exportEvents()}
              className="inline-flex items-center gap-2 rounded-md border border-slate-300 px-2 py-1 text-sm font-semibold text-slate-700 hover:bg-slate-50"
            >
              <Icon icon="mdi:download" className="h-4 w-4" />
              Export CSV
            </button>
          </div>
        </div>

        <div className="mb-3 flex max-h-24 flex-wrap gap-2 overflow-auto">
          {eventTypes.map((eventType) => (
            <label key={eventType} className="inline-flex items-center gap-1 rounded-md border border-slate-300 px-2 py-1 text-xs text-slate-700">
              <input
                type="checkbox"
                checked={selectedEventTypes.includes(eventType)}
                onChange={(event) => setSelectedEventTypes((current) => event.target.checked
                  ? [...current, eventType]
                  : current.filter((candidate) => candidate !== eventType))}
              />
              {getEventLabel(eventType)}
            </label>
          ))}
        </div>

        <div className="max-h-[520px] space-y-2 overflow-auto">
          {visibleEvents.map((event) => (
            <div key={event.id} className="rounded-md border border-slate-200 p-3">
              <div className="flex items-center justify-between gap-3">
                <p className="font-medium">{getEventLabel(event.eventType)}</p>
                <span className="text-xs text-slate-500">#{event.seq}</span>
              </div>
              <p className="text-xs text-slate-500">{formatDate(getEventChronologyTimestamp(event))} {event.validationFlags ? `· ${event.validationFlags}` : ''}</p>
              <p className="mt-1 text-sm text-slate-700">{getEventDetail(event)}</p>
            </div>
          ))}
        </div>
      </section>
    </section>
  )
}

function CollapsibleSection({ title, children, defaultOpen = false }: { title: string; children: ReactNode; defaultOpen?: boolean }) {
  return (
    <details className="group rounded-lg border border-slate-300 bg-white" open={defaultOpen}>
      <summary className="flex list-none cursor-pointer select-none items-center justify-between px-4 py-3 font-semibold text-slate-900 [&::-webkit-details-marker]:hidden">
        {title}
        <Icon icon="mdi:chevron-down" className="h-5 w-5 text-slate-500 transition-transform group-open:rotate-180" />
      </summary>
      <div className="border-t border-slate-200 p-4">{children}</div>
    </details>
  )
}

function RiskBar({ score }: { score: number }) {
  const normalized = Number.isFinite(score) ? Math.max(0, Math.min(Math.round(score), 100)) : 0
  const tone = normalized >= 70 ? 'bg-rose-600' : normalized >= 35 ? 'bg-amber-500' : 'bg-emerald-600'

  return (
    <div className="min-w-40">
      <div className="mb-1 flex items-center justify-between gap-2 text-xs font-semibold text-slate-700">
        <span>Suspicion</span>
        <span>{normalized}%</span>
      </div>
      <div className="h-2 rounded bg-slate-200">
        <div className={`h-2 rounded ${tone}`} style={{ width: `${normalized}%` }} />
      </div>
    </div>
  )
}

function SelfReportBadge({ value }: { value: boolean | null }) {
  if (value === null) {
    return <p className="mt-2 text-xs text-slate-400">Studentul nu a răspuns la întrebarea de auto-raportare.</p>
  }

  return (
    <p className={`mt-2 inline-flex items-center gap-1 rounded px-2 py-1 text-xs font-semibold ${value ? 'bg-rose-50 text-rose-700' : 'bg-emerald-50 text-emerald-700'}`}>
      <Icon icon={value ? 'mdi:alert-circle' : 'mdi:check-circle'} className="h-4 w-4" />
      Auto-raportare: {value ? 'a recunoscut frauda' : 'nu a trișat'}
    </p>
  )
}

function getLiveStatusFromEvent(lastEventType: string, status: string, fallback: string) {
  if (status && status.toLowerCase() !== 'active') {
    return status.toLowerCase()
  }

  if (lastEventType === 'window_blur' || lastEventType === 'visibility_change') {
    return 'out_of_window'
  }

  if (lastEventType === 'window_focus' || lastEventType === 'fullscreen_enter') {
    return 'active'
  }

  return fallback
}

type JsonRecord = Record<string, unknown>

function getLiveState(payload: JsonRecord): JsonRecord | null {
  const nested = payload.liveState
  if (isJsonRecord(nested)) {
    return nested
  }

  return typeof payload.sessionId === 'string' ? payload : null
}

function applyDashboardLiveEvent(
  current: ExamDashboard | null,
  event: DashboardLiveEvent,
  payload: JsonRecord,
  liveState: JsonRecord,
): ExamDashboard | null {
  if (!current || current.examScheduleId !== event.examScheduleId) {
    return current
  }

  const existing = current.sessions.find((session) => session.sessionId === event.sessionId)
  const updatedSession = createLiveSessionRow(existing, event, liveState)
  const sessions = existing
    ? current.sessions.map((session) => session.sessionId === event.sessionId ? updatedSession : session)
    : [...current.sessions, updatedSession]
  const eventType = readString(payload, 'eventType')
  const eventFrequencies = event.type === 'behavior.event' && eventType
    ? incrementEventFrequency(current.eventFrequencies, eventType)
    : current.eventFrequencies

  return {
    ...current,
    sessions,
    riskDistribution: calculateRiskDistribution(sessions),
    eventFrequencies,
  }
}

function createLiveSessionRow(
  existing: DashboardSessionRow | undefined,
  event: DashboardLiveEvent,
  liveState: JsonRecord,
): DashboardSessionRow {
  const status = readString(liveState, 'status') ?? existing?.status ?? 'active'
  const lastEventType = readString(liveState, 'lastEventType') ?? event.type
  const reportedTotalEvents = readFiniteNumber(liveState, 'totalEvents') ?? 0

  return {
    sessionId: readString(liveState, 'sessionId') ?? event.sessionId,
    studentId: readString(liveState, 'studentId') ?? existing?.studentId ?? event.studentId,
    studentEmail: readString(liveState, 'studentEmail') ?? existing?.studentEmail ?? 'Unknown student',
    status,
    startedAtUtc: existing?.startedAtUtc ?? event.occurredAtUtc,
    lastActivityAtUtc: readString(liveState, 'lastActivityAtUtc') ?? existing?.lastActivityAtUtc ?? event.occurredAtUtc,
    riskScore: readFiniteNumber(liveState, 'riskScore') ?? existing?.riskScore ?? 0,
    totalEvents: Math.max(existing?.totalEvents ?? 0, reportedTotalEvents),
    liveStatus: getLiveStatusFromEvent(lastEventType, status, existing?.liveStatus ?? status),
  }
}

function mergeDashboardSnapshot(current: ExamDashboard | null, incoming: ExamDashboard): ExamDashboard {
  if (!current || current.examScheduleId !== incoming.examScheduleId) {
    return {
      ...incoming,
      sessions: [...incoming.sessions],
      riskDistribution: calculateRiskDistribution(incoming.sessions),
    }
  }

  const incomingById = new Map(incoming.sessions.map((session) => [session.sessionId, session]))
  const currentById = new Map(current.sessions.map((session) => [session.sessionId, session]))
  const sessionIds = new Set([...incomingById.keys(), ...currentById.keys()])
  const sessions = Array.from(sessionIds, (sessionId) => {
    const fresh = incomingById.get(sessionId)
    const live = currentById.get(sessionId)
    if (!fresh) {
      return live!
    }
    if (!live || !isSessionStateNewer(live, fresh)) {
      return fresh
    }

    return {
      ...fresh,
      status: live.status,
      riskScore: live.riskScore,
      totalEvents: Math.max(live.totalEvents, fresh.totalEvents),
      lastActivityAtUtc: live.lastActivityAtUtc,
      liveStatus: live.liveStatus,
    }
  })

  return {
    ...incoming,
    sessions,
    riskDistribution: calculateRiskDistribution(sessions),
    eventFrequencies: mergeEventFrequencies(current.eventFrequencies, incoming.eventFrequencies),
  }
}

function isSessionStateNewer(candidate: DashboardSessionRow, reference: DashboardSessionRow) {
  return candidate.totalEvents > reference.totalEvents ||
    parseTimestamp(candidate.lastActivityAtUtc) > parseTimestamp(reference.lastActivityAtUtc)
}

function calculateRiskDistribution(sessions: DashboardSessionRow[]) {
  return sessions.reduce((distribution, session) => {
    if (session.riskScore >= 70) {
      distribution.high += 1
    } else if (session.riskScore >= 35) {
      distribution.medium += 1
    } else {
      distribution.low += 1
    }

    return distribution
  }, { low: 0, medium: 0, high: 0 })
}

function incrementEventFrequency(frequencies: ExamDashboard['eventFrequencies'], eventType: string) {
  const existing = frequencies.find((frequency) => frequency.eventType === eventType)
  return existing
    ? frequencies.map((frequency) => frequency.eventType === eventType
        ? { ...frequency, count: frequency.count + 1 }
        : frequency)
    : [...frequencies, { eventType, count: 1 }]
}

function mergeEventFrequencies(
  current: ExamDashboard['eventFrequencies'],
  incoming: ExamDashboard['eventFrequencies'],
) {
  const counts = new Map(incoming.map((frequency) => [frequency.eventType, frequency.count]))
  current.forEach((frequency) => {
    counts.set(frequency.eventType, Math.max(counts.get(frequency.eventType) ?? 0, frequency.count))
  })

  return Array.from(counts, ([eventType, count]) => ({ eventType, count }))
    .sort((left, right) => right.count - left.count || left.eventType.localeCompare(right.eventType))
}

function readString(record: JsonRecord, key: string) {
  const value = record[key]
  return typeof value === 'string' && value.length > 0 ? value : null
}

function readFiniteNumber(record: JsonRecord, key: string) {
  const value = record[key]
  return typeof value === 'number' && Number.isFinite(value) ? value : null
}

const eventLabels: Record<string, string> = {
  window_blur: 'Window blur',
  window_focus: 'Window focus',
  visibility_change: 'Visibility change',
  fullscreen_exit: 'Fullscreen exit',
  fullscreen_enter: 'Fullscreen enter',
  window_resize: 'Window resize',
  copy: 'Copy',
  cut: 'Cut',
  paste: 'Paste',
  clipboard_copy: 'Copy',
  clipboard_cut: 'Cut',
  clipboard_paste: 'Paste',
  text_burst_detected: 'Text burst detected',
  right_click: 'Right click',
  keyboard_shortcut: 'Keyboard shortcut',
  typing_speed_sample: 'Typing speed',
  devtools_heuristic_triggered: 'Devtools heuristic',
  print_attempt: 'Print attempt',
  question_view_start: 'Question view start',
  question_view_end: 'Question view end',
  question_navigation: 'Question navigation',
  answer_change: 'Answer change',
  answer_saved: 'Answer saved',
  idle_detected: 'Idle detected',
  session_start: 'Session start',
  session_end: 'Session end',
  session_resume: 'Session resume',
  multiple_tabs_detected: 'Multiple tabs detected',
  page_reload: 'Page reload',
  page_unload: 'Page unload',
  event_sequence_gap: 'Sequence gap',
  event_validation_failed: 'Validation failed',
  event_silence_detected: 'Event silence detected',
}

function getEventLabel(eventType: string) {
  return eventLabels[eventType] ?? eventType.replaceAll('_', ' ')
}

function getEventDetail(event: TimelineEvent) {
  const payload = flattenEventPayload(parsePayload(event.payloadJson))
  const parts: string[] = []
  const consumed = new Set(['eventType', 'questionId', 'details', 'data', 'payload'])

  const appendString = (key: string, label?: string) => {
    const value = readString(payload, key)
    if (value) {
      parts.push(label ? `${label} ${value}` : value)
      consumed.add(key)
    }
  }
  const appendNumber = (key: string, render: (value: number) => string) => {
    const value = readFiniteNumber(payload, key)
    if (value !== null) {
      parts.push(render(value))
      consumed.add(key)
    }
  }

  appendNumber('questionIndex', (value) => `question ${value + 1}`)
  appendNumber('orderIndex', (value) => `order ${value}`)
  appendString('questionType', 'type')
  appendString('visibilityState')

  if (typeof payload.hidden === 'boolean') {
    parts.push(payload.hidden ? 'hidden' : 'visible')
    consumed.add('hidden')
  }

  appendString('activeElement', 'element')
  appendNumber('blurDurationMs', (value) => `blur ${formatDuration(value)}`)
  appendNumber('durationMs', (value) => `duration ${formatDuration(value)}`)
  appendNumber('idleMs', (value) => `idle ${formatDuration(value)}`)
  appendNumber('silentForMs', (value) => `silent ${formatDuration(value)}`)
  appendNumber('elapsedMs', (value) => `change in ${formatDuration(value)}`)
  appendNumber('sampleMs', (value) => `sample ${formatDuration(value)}`)
  appendNumber('textLength', (value) => `${value} chars`)
  appendNumber('charDelta', (value) => `${value >= 0 ? '+' : ''}${value} chars`)
  appendNumber('answerLength', (value) => `answer ${value} chars`)
  appendNumber('changeCount', (value) => `${value} changes`)
  appendNumber('selectedCount', (value) => `${value} selected`)
  appendNumber('charsPerSecond', (value) => `${value.toFixed(1)} chars/sec`)
  appendString('shortcut')
  appendString('direction', 'navigation')
  appendString('source', 'source')
  appendString('reason', 'reason')
  appendString('detector', 'detector')

  const fromIndex = readFiniteNumber(payload, 'fromIndex')
  const toIndex = readFiniteNumber(payload, 'toIndex')
  if (fromIndex !== null || toIndex !== null) {
    parts.push(`question ${(fromIndex ?? -1) + 1} → ${(toIndex ?? -1) + 1}`)
    consumed.add('fromIndex')
    consumed.add('toIndex')
    consumed.add('fromQuestionId')
    consumed.add('toQuestionId')
  }

  const width = readFiniteNumber(payload, 'width')
  const height = readFiniteNumber(payload, 'height')
  if (width !== null || height !== null) {
    parts.push(`viewport ${width ?? '?'}×${height ?? '?'}`)
    consumed.add('width')
    consumed.add('height')
  }

  const widthDelta = readFiniteNumber(payload, 'widthDelta')
  const heightDelta = readFiniteNumber(payload, 'heightDelta')
  if (widthDelta !== null || heightDelta !== null) {
    parts.push(`resize Δ${widthDelta ?? 0}×${heightDelta ?? 0}`)
    consumed.add('widthDelta')
    consumed.add('heightDelta')
  }

  const widthGap = readFiniteNumber(payload, 'widthGap')
  const heightGap = readFiniteNumber(payload, 'heightGap')
  if (widthGap !== null || heightGap !== null) {
    parts.push(`window gap ${widthGap ?? 0}×${heightGap ?? 0}`)
    consumed.add('widthGap')
    consumed.add('heightGap')
  }

  const x = readFiniteNumber(payload, 'x')
  const y = readFiniteNumber(payload, 'y')
  if (x !== null || y !== null) {
    parts.push(`position ${x ?? '?'}×${y ?? '?'}`)
    consumed.add('x')
    consumed.add('y')
  }

  appendNumber('clientSeq', (value) => `client seq ${value}`)
  appendNumber('observedSeq', (value) => `observed seq ${value}`)
  appendNumber('previousSeq', (value) => `previous seq ${value}`)
  appendNumber('gap', (value) => `gap ${value}`)
  appendNumber('threshold', (value) => `threshold ${value}`)
  appendString('lastActivityAtUtc', 'last activity')

  for (const [key, value] of Object.entries(payload)) {
    if (consumed.has(key) || value === null || value === undefined) {
      continue
    }

    const formatted = formatPayloadValue(value)
    if (formatted) {
      parts.push(`${humanizePayloadKey(key)}: ${formatted}`)
    }
  }

  return parts.length > 0 ? parts.join(' · ') : 'Recorded behavioral signal'
}

function parsePayload(payloadJson: string): JsonRecord {
  try {
    const payload: unknown = JSON.parse(payloadJson)
    const normalized = normalizeJsonValue(payload)
    return isJsonRecord(normalized) ? normalized : {}
  } catch {
    return {}
  }
}

function flattenEventPayload(payload: JsonRecord) {
  const nestedPayload = isJsonRecord(payload.payload) ? payload.payload : {}
  const nestedData = isJsonRecord(payload.data) ? payload.data : {}
  const details = isJsonRecord(payload.details) ? payload.details : {}
  return { ...payload, ...nestedPayload, ...nestedData, ...details }
}

function normalizeJsonValue(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(normalizeJsonValue)
  }

  if (!isJsonRecord(value)) {
    return value
  }

  return Object.fromEntries(
    Object.entries(value).map(([key, nested]) => [normalizeJsonKey(key), normalizeJsonValue(nested)]),
  )
}

function normalizeJsonKey(key: string) {
  return key.length === 0 ? key : `${key[0].toLowerCase()}${key.slice(1)}`
}

function isJsonRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function formatPayloadValue(value: unknown) {
  if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
    return String(value)
  }

  try {
    return JSON.stringify(value)
  } catch {
    return ''
  }
}

function humanizePayloadKey(key: string) {
  return key
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replaceAll('_', ' ')
    .toLowerCase()
}

function filterTimelineEventsForQuestion(events: TimelineEvent[], questionId: string) {
  const intervals = buildQuestionIntervals(events, questionId)

  return events.filter((event) => {
    if (event.questionId === questionId) {
      return true
    }

    return !event.questionId && intervals.some((interval) => {
      const timestamp = parseTimestamp(getEventChronologyTimestamp(event))
      return timestamp >= interval.start && timestamp <= interval.end
    })
  })
}

function buildQuestionIntervals(events: TimelineEvent[], questionId: string) {
  const intervals: Array<{ start: number; end: number }> = []
  let currentStart: number | null = null

  events
    .filter((event) => event.questionId === questionId)
    .sort(compareTimelineEvents)
    .forEach((event) => {
      if (event.eventType === 'question_view_start') {
        currentStart = parseTimestamp(getEventChronologyTimestamp(event))
      }

      if (event.eventType === 'question_view_end' && currentStart !== null) {
        intervals.push({ start: currentStart, end: parseTimestamp(getEventChronologyTimestamp(event)) })
        currentStart = null
      }
    })

  if (currentStart !== null) {
    intervals.push({ start: currentStart, end: Number.MAX_SAFE_INTEGER })
  }

  return intervals
}

function sortSessionTimeline(timeline: SessionTimeline): SessionTimeline {
  return {
    ...timeline,
    questions: [...timeline.questions].sort((left, right) => left.orderIndex - right.orderIndex),
    events: sortTimelineEvents(timeline.events),
    riskSnapshots: [...timeline.riskSnapshots].sort((left, right) =>
      parseTimestamp(left.createdAtUtc) - parseTimestamp(right.createdAtUtc)),
  }
}

function sortTimelineEvents(events: TimelineEvent[]) {
  return [...events].sort(compareTimelineEvents)
}

function compareTimelineEvents(left: TimelineEvent, right: TimelineEvent) {
  return parseTimestamp(getEventChronologyTimestamp(left)) - parseTimestamp(getEventChronologyTimestamp(right)) ||
    parseTimestamp(left.timestampUtc) - parseTimestamp(right.timestampUtc) ||
    left.seq - right.seq
}

function getEventChronologyTimestamp(event: TimelineEvent) {
  return event.receivedAtUtc || event.timestampUtc
}

function parseTimestamp(value: string) {
  const timestamp = Date.parse(value)
  return Number.isFinite(timestamp) ? timestamp : 0
}

function createEventsCsv(events: TimelineEvent[]) {
  const rows = [
    ['Seq', 'ClientTimestampUtc', 'ReceivedAtUtc', 'QuestionId', 'EventType', 'ValidationFlags', 'PayloadJson'],
    ...events.map((event) => [
      String(event.seq),
      event.timestampUtc,
      event.receivedAtUtc ?? '',
      event.questionId ?? '',
      event.eventType,
      event.validationFlags,
      event.payloadJson,
    ]),
  ]

  return rows.map((row) => row.map(escapeCsv).join(',')).join('\n')
}

function escapeCsv(value: string) {
  return `"${value.replaceAll('"', '""')}"`
}

function formatDuration(ms: number) {
  if (ms < 1000) {
    return `${Math.round(ms)}ms`
  }

  return `${(ms / 1000).toFixed(1)}s`
}

function Metric({ label, value, tone }: { label: string; value: number; tone: 'emerald' | 'amber' | 'rose' }) {
  const toneClass = tone === 'emerald' ? 'text-emerald-800 bg-emerald-50' : tone === 'amber' ? 'text-amber-800 bg-amber-50' : 'text-rose-800 bg-rose-50'

  return (
    <div className={`rounded-lg border border-slate-300 p-4 ${toneClass}`}>
      <p className="text-sm font-medium">{label}</p>
      <p className="mt-1 text-3xl font-semibold">{value}</p>
    </div>
  )
}

async function fetchReferenceData() {
  const [assessmentResponse, examResponse] = await Promise.all([
    api.get<AssessmentSummary[]>('/api/assessments'),
    api.get<ExamSchedule[]>('/api/exams'),
  ])

  return {
    assessments: assessmentResponse.data,
    exams: examResponse.data,
  }
}

async function fetchExamDashboard(examId: string, signal: AbortSignal) {
  const response = await api.get<ExamDashboard>(`/api/dashboard/exams/${examId}`, { signal })
  return response.data
}

async function fetchSessionTimeline(sessionId: string, signal: AbortSignal) {
  const response = await api.get<SessionTimeline>(`/api/dashboard/sessions/${sessionId}/timeline`, { signal })
  return response.data
}

async function fetchAssessment(assessmentId: string, signal: AbortSignal) {
  const response = await api.get<Assessment>(`/api/assessments/${assessmentId}`, { signal })
  return response.data
}

async function streamDashboard(examId: string, signal: AbortSignal, onEvent: (event: DashboardLiveEvent) => void) {
  let retryDelayMs = 500

  while (!signal.aborted) {
    let receivedEvent = false
    try {
      const token = getAccessToken()
      const response = await fetch(resolveApiUrl(`/api/dashboard/exams/${examId}/stream`), {
        headers: {
          Accept: 'text/event-stream',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        signal,
      })

      if (!response.ok) {
        throw new Error(`Dashboard stream request failed with status ${response.status}.`)
      }

      receivedEvent = await consumeDashboardStream(response, signal, onEvent)
    } catch {
      if (signal.aborted) {
        return
      }
    }

    if (signal.aborted) {
      return
    }

    await waitForRetry(retryDelayMs, signal)
    retryDelayMs = receivedEvent ? 500 : Math.min(retryDelayMs * 2, 15_000)
  }
}

async function consumeDashboardStream(
  response: Response,
  signal: AbortSignal,
  onEvent: (event: DashboardLiveEvent) => void,
) {
  const reader = response.body?.getReader()
  if (!reader) {
    throw new Error('Dashboard stream response did not include a body.')
  }

  const decoder = new TextDecoder()
  let buffer = ''
  let receivedEvent = false

  try {
    while (!signal.aborted) {
      const result = await reader.read()
      buffer += decoder.decode(result.value, { stream: !result.done })
      const frames = buffer.split(/\r?\n\r?\n/)
      buffer = frames.pop() ?? ''

      frames.forEach((frame) => {
        receivedEvent = dispatchDashboardFrame(frame, onEvent) || receivedEvent
      })

      if (result.done) {
        if (buffer.trim()) {
          receivedEvent = dispatchDashboardFrame(buffer, onEvent) || receivedEvent
        }
        return receivedEvent
      }
    }
  } finally {
    reader.releaseLock()
  }

  return receivedEvent
}

function dispatchDashboardFrame(frame: string, onEvent: (event: DashboardLiveEvent) => void) {
  const lines = frame.split(/\r?\n/)
  const eventName = lines.find((line) => line.startsWith('event:'))?.slice(6).trim() ?? ''
  const data = lines
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice(5).trimStart())
    .join('\n')

  if (!data) {
    return false
  }

  try {
    const parsed: unknown = JSON.parse(data)
    const event = normalizeDashboardLiveEvent(parsed, eventName)
    if (!event) {
      return false
    }

    onEvent(event)
    return true
  } catch {
    return false
  }
}

function normalizeDashboardLiveEvent(value: unknown, eventName: string): DashboardLiveEvent | null {
  const normalized = normalizeJsonValue(value)
  if (!isJsonRecord(normalized)) {
    return null
  }

  const examScheduleId = readString(normalized, 'examScheduleId')
  const sessionId = readString(normalized, 'sessionId')
  const studentId = readString(normalized, 'studentId')
  const type = readString(normalized, 'type') ?? eventName
  const occurredAtUtc = readString(normalized, 'occurredAtUtc')
  const rawPayload = normalized.payloadJson
  const payloadJson = typeof rawPayload === 'string'
    ? rawPayload
    : isJsonRecord(rawPayload) || Array.isArray(rawPayload)
      ? JSON.stringify(rawPayload)
      : ''

  if (!examScheduleId || !sessionId || !studentId || !type || !occurredAtUtc || !payloadJson) {
    return null
  }

  return { examScheduleId, sessionId, studentId, type, payloadJson, occurredAtUtc }
}

function waitForRetry(delayMs: number, signal: AbortSignal) {
  return new Promise<void>((resolve) => {
    if (signal.aborted) {
      resolve()
      return
    }

    const finish = () => {
      window.clearTimeout(timer)
      signal.removeEventListener('abort', finish)
      resolve()
    }
    const timer = window.setTimeout(finish, delayMs)
    signal.addEventListener('abort', finish, { once: true })
  })
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' }).format(date)
}
