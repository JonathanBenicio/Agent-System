import { Outlet } from 'react-router-dom'
import { useAuthStore } from '../../store/authStore'
import { LoginModal } from './LoginModal'
import { PageLoading } from '../shared/Loading'

export function ProtectedRoute() {
  const { isAuthenticated, isLoading } = useAuthStore()

  if (isLoading) {
    return <PageLoading />
  }

  if (!isAuthenticated) {
    return <LoginModal />
  }

  return <Outlet />
}
