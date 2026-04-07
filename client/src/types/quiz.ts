export type QuestionType = 'single' | 'multiple' | 'true-false' | 'text'

export type QuestionOption = {
  id: string
  label: string
}

export type ChoiceQuestion = {
  id: string
  type: Exclude<QuestionType, 'text'>
  topic: string
  prompt: string
  options: QuestionOption[]
}

export type TextQuestion = {
  id: string
  type: 'text'
  topic: string
  prompt: string
  placeholder: string
}

export type Question = ChoiceQuestion | TextQuestion

export type AnswerValue = string | string[]

export type AnswerRecord = Record<string, AnswerValue>
