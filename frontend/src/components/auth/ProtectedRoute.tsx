import { Outlet } from 'react-router-dom'
import { useAuthStore } from '../../store/authStore'
import { LoginModal } from './LoginModal'

export function ProtectedRoute() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated)

  if (!isAuthenticated) {
    return <LoginModal />
  }

  return <Outlet />
}
