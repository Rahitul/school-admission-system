import { render, screen, waitFor } from '@testing-library/react'
import App from './App'

const mockClass = {
  id: 'c_1',
  name: 'Class 6 - Morning Shift',
  totalSeats: 120,
  heldSeats: 15,
  confirmedSeats: 80,
  availableSeats: 25,
}

describe('App', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => [mockClass],
    }))
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders the header and class seat counts', async () => {
    render(<App />)
    expect(screen.getByText('School Admission System')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByText('Class 6 - Morning Shift')).toBeInTheDocument())
    expect(screen.getByText('Total: 120')).toBeInTheDocument()
    expect(screen.getByText('Available: 25')).toBeInTheDocument()
  })

  it('shows empty state when no classes', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => [],
    }))
    render(<App />)
    await waitFor(() => expect(screen.getByText('No classes yet.')).toBeInTheDocument())
  })
})