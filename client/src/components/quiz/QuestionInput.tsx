import type { AnswerValue, Question } from '../../types/quiz'

type QuestionInputProps = {
  question: Question
  answer: AnswerValue | undefined
  onChange: (value: AnswerValue) => void
}

export function QuestionInput({ question, answer, onChange }: QuestionInputProps) {
  if (question.type === 'text') {
    return (
      <div>
        <label htmlFor={question.id} className="sr-only">
          {question.prompt}
        </label>
        <textarea
          id={question.id}
          value={typeof answer === 'string' ? answer : ''}
          onChange={(event) => onChange(event.target.value)}
          rows={6}
          placeholder={question.placeholder}
          className="min-h-40 w-full resize-y rounded-lg border border-slate-200 bg-white px-4 py-3 text-base leading-7 text-slate-900 shadow-sm transition placeholder:text-slate-400 focus:border-emerald-700 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 dark:border-slate-800 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500 dark:focus:border-emerald-400 dark:focus:ring-emerald-400 dark:focus:ring-offset-slate-900"
        />
      </div>
    )
  }

  const isMultiple = question.type === 'multiple'
  const selectedOptions = Array.isArray(answer) ? answer : []
  const selectedOption = typeof answer === 'string' ? answer : ''

  function handleChoice(optionId: string) {
    if (!isMultiple) {
      onChange(optionId)
      return
    }

    const nextValue = selectedOptions.includes(optionId)
      ? selectedOptions.filter((selected) => selected !== optionId)
      : [...selectedOptions, optionId]

    onChange(nextValue)
  }

  return (
    <fieldset className="space-y-3">
      <legend className="sr-only">{question.prompt}</legend>
      {question.options.map((option) => {
        const isSelected = isMultiple ? selectedOptions.includes(option.id) : selectedOption === option.id
        const inputId = `${question.id}-${option.id}`

        return (
          <label
            key={option.id}
            htmlFor={inputId}
            className={`flex cursor-pointer items-start gap-3 rounded-lg border p-4 text-left transition focus-within:ring-2 focus-within:ring-emerald-600 focus-within:ring-offset-2 dark:focus-within:ring-emerald-400 dark:focus-within:ring-offset-slate-900 ${
              isSelected
                ? 'border-emerald-700 bg-emerald-50 text-slate-950 dark:border-emerald-400 dark:bg-emerald-950/40 dark:text-slate-50'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50 dark:border-slate-800 dark:bg-slate-950 dark:text-slate-300 dark:hover:border-slate-700 dark:hover:bg-slate-900'
            }`}
          >
            <input
              id={inputId}
              name={question.id}
              type={isMultiple ? 'checkbox' : 'radio'}
              checked={isSelected}
              onChange={() => handleChoice(option.id)}
              className="mt-1 h-4 w-4 border-slate-300 accent-emerald-700 dark:border-slate-700 dark:accent-emerald-400"
            />
            <span className="text-base leading-6">{option.label}</span>
          </label>
        )
      })}
    </fieldset>
  )
}
