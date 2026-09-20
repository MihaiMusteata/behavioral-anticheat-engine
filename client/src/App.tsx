import { useState } from 'react'
import { ToastContainer } from 'react-toastify'
import { AuthPage } from './features/auth/AuthPage'
import { AuthProvider } from './features/auth/AuthContext'
import { useAuth } from './features/auth/auth-context'
import { AdminApp } from './features/dashboard/AdminApp'
import { JoinExamPage } from './features/exams/JoinExamPage'
import { StudentApp } from './features/exams/StudentApp'

function App() {
  return (
    <AuthProvider>
      <AppRoutes />
      <ToastContainer position="bottom-right" autoClose={2400} closeOnClick hideProgressBar newestOnTop pauseOnFocusLoss={false} />
    </AuthProvider>
  )
}

function AppRoutes() {
  const { user } = useAuth()
  // Staff login is intentionally not linked from the public join form - students
  // should only ever see the PIN/name screen. Admins/proctors reach it via a
  // direct ?staff link instead, kept out of the student-facing page.
  const [showStaffLogin, setShowStaffLogin] = useState(() => new URLSearchParams(window.location.search).has('staff'))

  if (!user) {
    return showStaffLogin
      ? <AuthPage onBackToJoin={() => setShowStaffLogin(false)} />
      : <JoinExamPage onJoined={() => setShowStaffLogin(false)} />
  }

  return user.role === 'Student' ? <StudentApp /> : <AdminApp />
}

export default App
