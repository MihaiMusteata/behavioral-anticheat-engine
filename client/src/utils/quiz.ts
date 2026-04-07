import type { AnswerValue, Question } from '../types/quiz'

export const questionTypeLabels: Record<Question['type'], string> = {
  single: 'Single choice',
  multiple: 'Multiple choice',
  'true-false': 'True/False',
  text: 'Text answer',
}

export function isQuestionAnswered(question: Question, answer: AnswerValue | undefined) {
  if (question.type === 'multiple') {
    return Array.isArray(answer) && answer.length > 0
  }

  if (question.type === 'text') {
    return typeof answer === 'string' && answer.trim().length > 0
  }

  return typeof answer === 'string' && answer.length > 0
}
