import { useEffect, useState } from 'react'
import { toast, ToastContainer } from 'react-toastify'
import { AppHeader } from '../components/AppHeader'
import { CompletionPanel } from '../components/quiz/CompletionPanel'
import { QuestionCard } from '../components/quiz/QuestionCard'
import { questions } from '../data/questions'
import { isQuestionAnswered } from '../utils/quiz'
import type { AnswerRecord } from '../types/quiz'

const themeStorageKey = 'academic-quiz-theme'

function getInitialDarkMode() {
  const savedTheme = window.localStorage.getItem(themeStorageKey)

  if (savedTheme === 'dark') {
    return true
  }

  if (savedTheme === 'light') {
    return false
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

export function QuizPage() {
  const [currentIndex, setCurrentIndex] = useState(0)
  const [answers, setAnswers] = useState<AnswerRecord>({})
  const [isComplete, setIsComplete] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [isDarkMode, setIsDarkMode] = useState(getInitialDarkMode)

  const currentQuestion = questions[currentIndex]
  const currentAnswer = answers[currentQuestion.id]
  const currentQuestionNumber = currentIndex + 1
  const isLastQuestion = currentQuestionNumber === questions.length

  useEffect(() => {
    document.documentElement.classList.toggle('dark', isDarkMode)
    window.localStorage.setItem(themeStorageKey, isDarkMode ? 'dark' : 'light')
  }, [isDarkMode])

  function handleAdvance() {
    if (!isQuestionAnswered(currentQuestion, currentAnswer)) {
      toast.warn('Please answer the current question before continuing.')
      return
    }

    if (isLastQuestion) {
      setIsComplete(true)
      return
    }

    setCurrentIndex((index) => index + 1)
  }

  function handleSubmit() {
    setSubmitted(true)
    toast.success('Quiz submitted successfully.')
  }

  return (
    <div className="min-h-dvh bg-slate-50 text-slate-950 transition-colors dark:bg-slate-950 dark:text-slate-100">
      <AppHeader isDarkMode={isDarkMode} onToggleTheme={() => setIsDarkMode((value) => !value)} />

      <main className="mx-auto flex w-full max-w-6xl px-4 py-6 sm:px-6 sm:py-8 lg:px-8">
        <div className="mx-auto w-full max-w-4xl">
          {isComplete ? (
            <CompletionPanel submitted={submitted} onSubmit={handleSubmit} />
          ) : (
            <QuestionCard
              answer={currentAnswer}
              currentQuestionNumber={currentQuestionNumber}
              isLastQuestion={isLastQuestion}
              onAnswerChange={(value) =>
                setAnswers((currentAnswers) => ({
                  ...currentAnswers,
                  [currentQuestion.id]: value,
                }))
              }
              onNext={handleAdvance}
              question={currentQuestion}
              totalQuestions={questions.length}
            />
          )}
        </div>
      </main>

      <ToastContainer
        position="bottom-right"
        autoClose={2400}
        closeOnClick
        hideProgressBar
        newestOnTop
        pauseOnFocusLoss={false}
        theme={isDarkMode ? 'dark' : 'light'}
      />
    </div>
  )
}
