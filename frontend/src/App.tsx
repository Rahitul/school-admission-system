import { useCallback, useEffect, useRef, useState } from 'react'
import './App.css'

const API_BASE = (import.meta.env.VITE_API_BASE_URL as string) || 'http://localhost:8080'

type ClassSummary = {
  id: string
  name: string
  totalSeats: number
  heldSeats: number
  confirmedSeats: number
  availableSeats: number
}

type Hold = {
  holdId: string
  classId: string
  studentId?: string
  status: string
  expiresAt: string
}

type MyHold = {
  holdId: string
  classId: string
  className: string
  status: string
  expiresAt: string
}

type ApiError = { error?: { code?: string; message?: string } }

async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json', ...(options.headers as Record<string, string> | undefined) }
  let res: Response
  try {
    res = await fetch(API_BASE + path, { ...options, headers })
  } catch {
    throw new Error('Cannot reach the server.')
  }
  if (!res.ok) {
    let msg = `Request failed (${res.status})`
    try {
      const body = (await res.json()) as ApiError
      if (body?.error?.message) msg = body.error.message
    } catch {
      // ignore
    }
    const err = new Error(msg) as Error & { status: number }
    err.status = res.status
    throw err
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

function isValidStudentId(v: string) {
  return /^\d{17}$/.test(v)
}

const BIRTH_LABEL = 'Birth registration number (17 digits): '
const BIRTH_ERROR = 'Birth registration number must be 17 digits.'
const APPLY_LABEL = 'Apply'
const PAY_LABEL = 'Pay admission fee'
const CANCEL_LABEL = 'Cancel'
const PAY_SUCCESS = 'Payment successful. Admission confirmed.'
const CANCEL_SUCCESS = 'Application cancelled.'

export default function App() {
  const [studentId, setStudentId] = useState(() => localStorage.getItem('studentId') || '')
  const [studentInput, setStudentInput] = useState(studentId)
  const [studentError, setStudentError] = useState('')
  const [tab, setTab] = useState<'classes' | 'mine' | 'create'>('classes')
  const [classes, setClasses] = useState<ClassSummary[]>([])
  const [myHolds, setMyHolds] = useState<MyHold[]>([])
  const [selectedHoldId, setSelectedHoldId] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const studentIdRef = useRef(studentId)
  useEffect(() => { studentIdRef.current = studentId }, [studentId])

  const saveStudentId = () => {
    const v = studentInput.trim()
    if (!isValidStudentId(v)) {
      setStudentError(BIRTH_ERROR)
      return
    }
    setStudentError('')
    setStudentId(v)
    localStorage.setItem('studentId', v)
    setMessage('Student ID saved.')
  }

  const loadClasses = useCallback(async () => {
    try {
      const data = await api<ClassSummary[]>('/classes')
      setClasses(data)
    } catch (e: unknown) {
      setError((e as Error)?.message || 'Cannot reach the server.')
    }
  }, [])

  const loadMyHolds = useCallback(async () => {
    const sid = studentIdRef.current
    if (!isValidStudentId(sid)) return
    try {
      const data = await api<MyHold[]>('/me/holds', { headers: { 'X-Student-Id': sid } })
      setMyHolds(data)
    } catch (e: unknown) {
      setError((e as Error)?.message || 'Cannot reach the server.')
    }
  }, [])

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadClasses()
    const t = setInterval(loadClasses, 4000)
    return () => clearInterval(t)
  }, [loadClasses])

  useEffect(() => {
    if (isValidStudentId(studentId)) {
      loadMyHolds()
      const t = setInterval(loadMyHolds, 5000)
      return () => clearInterval(t)
    }
  }, [studentId, loadMyHolds])

  const activeClassIds = new Set(
    myHolds.filter(h => h.status === 'HELD' || h.status === 'CONFIRMED').map(h => h.classId)
  )
  const heldClassIds = new Set(
    myHolds.filter(h => h.status === 'HELD').map(h => h.classId)
  )
  const confirmedClassIds = new Set(
    myHolds.filter(h => h.status === 'CONFIRMED').map(h => h.classId)
  )

  const handleApply = async (classId: string) => {
    if (!isValidStudentId(studentId)) {
      setError('Please enter a valid 17-digit student ID first.')
      return
    }
    setLoading(true)
    setError('')
    setMessage('')
    try {
      const res = await api<Hold>('/classes/' + classId + '/holds', {
        method: 'POST',
        headers: { 'X-Student-Id': studentId }
      })
      setSelectedHoldId(res.holdId)
      setMessage('Application created.')
      await loadClasses()
      await loadMyHolds()
    } catch (e: unknown) {
      setError((e as Error)?.message || 'Cannot reach the server.')
    } finally {
      setLoading(false)
    }
  }

  const handleCreateClass = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const form = e.currentTarget
    const name = (form.elements.namedItem('name') as HTMLInputElement).value.trim()
    const seats = parseInt((form.elements.namedItem('seats') as HTMLInputElement).value, 10)
    if (!name) { setError('Class name is required.'); return }
    if (!seats || seats < 1) { setError('Total seats must be at least 1.'); return }
    setLoading(true)
    setError('')
    setMessage('')
    try {
      await api('/classes', { method: 'POST', body: JSON.stringify({ name, totalSeats: seats }) })
      setMessage('Class created.')
      form.reset()
      await loadClasses()
      setTab('classes')
    } catch (e: unknown) {
      setError((e as Error)?.message || 'Cannot reach the server.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="app">
      <header className="header">
        <h1>School Admission System</h1>
        <div className="student-box">
          <label htmlFor="studentId">{BIRTH_LABEL}</label>
          <input
            id="studentId"
            value={studentInput}
            onChange={(e) => setStudentInput(e.target.value)}
            placeholder="17-digit birth registration number"
            maxLength={17}
          />
          <button onClick={saveStudentId}>Save</button>
          {studentError && <span className="err">{studentError}</span>}
        </div>
      </header>

      {loading && <div className="msg">Loading...</div>}
      {message && <div className="msg ok">{message}</div>}
      {error && <div className="msg err">{error}</div>}

      <nav className="nav">
        <button className={tab === 'classes' ? 'active' : ''} onClick={() => setTab('classes')}>Classes</button>
        <button className={tab === 'mine' ? 'active' : ''} onClick={() => setTab('mine')}>My Applications</button>
        <button className={tab === 'create' ? 'active' : ''} onClick={() => setTab('create')}>Create Class</button>
      </nav>

      <main>
        {tab === 'classes' && (
          <section>
            {classes.length === 0 && <p>No classes yet.</p>}
            <div className="class-grid">
              {classes.map((c) => {
                const already = activeClassIds.has(c.id)
                return (
                  <div key={c.id} className="class-card">
                    <h3>{c.name}</h3>
                    <p>Total: {c.totalSeats}</p>
                    <p>Held: {c.heldSeats}</p>
                    <p>Confirmed: {c.confirmedSeats}</p>
                    <p>Available: {c.availableSeats}</p>
                    <button
                      disabled={loading || c.availableSeats <= 0 || already || !isValidStudentId(studentId)}
                      onClick={() => handleApply(c.id)}
                    >
                      {APPLY_LABEL}
                    </button>
                    {heldClassIds.has(c.id) && <small>You are on hold in this class.</small>}
                    {confirmedClassIds.has(c.id) && !heldClassIds.has(c.id) && <small>You have already applied to this class.</small>}
                    {c.availableSeats <= 0 && <small>No seats left.</small>}
                  </div>
                )
              })}
            </div>
          </section>
        )}

        {tab === 'mine' && (
          <section>
            {!isValidStudentId(studentId) && <p>Enter a valid student ID to see your applications.</p>}
            {isValidStudentId(studentId) && myHolds.length === 0 && <p>No applications yet.</p>}
            <ul className="hold-list">
              {myHolds.map((h) => (
                <li key={h.holdId}>
                  <span>{h.className}</span>
                  <span>Status: {h.status}</span>
                  <span>Expires: {new Date(h.expiresAt).toLocaleString()}</span>
                  <button onClick={() => setSelectedHoldId(h.holdId)}>View</button>
                </li>
              ))}
            </ul>
          </section>
        )}

        {tab === 'create' && (
          <section>
            <form onSubmit={handleCreateClass} className="form">
              <label>Class name: <input name="name" required /></label>
              <label>Total seats: <input name="seats" type="number" min={1} required /></label>
              <button type="submit" disabled={loading}>Create class</button>
            </form>
          </section>
        )}
      </main>

      {selectedHoldId && (
        <HoldDetail holdId={selectedHoldId} studentId={studentId} classes={classes} onClose={() => setSelectedHoldId(null)}
          onChanged={() => { loadClasses(); loadMyHolds() }} />
      )}
    </div>
  )
}

function HoldDetail({ holdId, studentId, classes, onClose, onChanged }: {
  holdId: string; studentId: string; classes: ClassSummary[]; onClose: () => void; onChanged: () => void
}) {
  const [hold, setHold] = useState<Hold | null>(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(false)
  // eslint-disable-next-line react-hooks/purity
  const [now, setNow] = useState(Date.now())

  const load = useCallback(async () => {
    try {
      const data = await api<Hold>('/holds/' + holdId)
      setHold(data)
    } catch (e: unknown) {
      setError((e as Error)?.message || 'Cannot reach the server.')
    }
  }, [holdId])

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load()
    const t = setInterval(load, 4000)
    return () => clearInterval(t)
  }, [load])

  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(t)
  }, [])

  const isExpired = !!hold && (hold.status === 'EXPIRED' || (hold.status === 'HELD' && new Date(hold.expiresAt).getTime() <= now))
  const status = hold ? (hold.status === 'HELD' && new Date(hold.expiresAt).getTime() <= now ? 'EXPIRED' : hold.status) : ''

  const handleConfirm = async () => {
    if (!isValidStudentId(studentId)) { setError('Invalid student ID.'); return }
    setLoading(true)
    setError('')
    setMessage('')
    try {
      await api('/holds/' + holdId + '/confirm', { method: 'POST', headers: { 'X-Student-Id': studentId } })
      setMessage(PAY_SUCCESS)
      await load()
      onChanged()
    } catch (e: unknown) {
      setError((e as Error)?.message || 'Cannot reach the server.')
    } finally {
      setLoading(false)
    }
  }

  const handleCancel = async () => {
    if (!isValidStudentId(studentId)) { setError('Invalid student ID.'); return }
    setLoading(true)
    setError('')
    setMessage('')
    try {
      await api('/holds/' + holdId, { method: 'DELETE', headers: { 'X-Student-Id': studentId } })
      setMessage(CANCEL_SUCCESS)
      await load()
      onChanged()
    } catch (e: unknown) {
      setError((e as Error)?.message || 'Cannot reach the server.')
    } finally {
      setLoading(false)
    }
  }

  const remaining = hold ? Math.max(0, new Date(hold.expiresAt).getTime() - now) : 0
  const mins = Math.floor(remaining / 60000)
  const secs = Math.floor((remaining % 60000) / 1000)

  return (
    <div className="modal">
      <div className="modal-content">
        <button className="close" onClick={onClose}>X</button>
        <h2>Application Detail</h2>
        {error && <div className="msg err">{error}</div>}
        {message && <div className="msg ok">{message}</div>}
        {!hold && <p>Loading...</p>}
        {hold && (
          <>
            <p>Hold ID: {hold.holdId}</p>
            <p>Class: {classes.find((c) => c.id === hold.classId)?.name || hold.classId}</p>
            <p>Status: {status}</p>
            <p>Expires: {new Date(hold.expiresAt).toLocaleString()}</p>
            {status === 'HELD' && !isExpired && (
              <p>Time remaining: {mins}:{secs.toString().padStart(2, '0')}</p>
            )}
            <div className="actions">
              <button disabled={loading || status !== 'HELD' || isExpired} onClick={handleConfirm}>
                {PAY_LABEL}
              </button>
              <button disabled={loading || status !== 'HELD'} onClick={handleCancel}>
                {CANCEL_LABEL}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}