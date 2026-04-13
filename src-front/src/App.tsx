import { startTransition, useDeferredValue, useEffect, useEffectEvent, useState, type FormEvent } from 'react'
import { getErrorMessage, jobsApi } from './api'
import type { CreateJobPayload, Job, Notice } from './types'
import {
  formatDate,
  formatPercent,
  isTerminalStatus,
  normalizeUuid,
  shortId,
  sortJobs,
  statusStyles,
} from './utils'

const defaultCreateForm = {
  duration: '00:00:15',
  shouldFail: false,
  timeout: '00:00:20',
}

export default function App() {
  const [jobs, setJobs] = useState<Job[]>([])
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null)
  const [notice, setNotice] = useState<Notice | null>(null)
  const [filter, setFilter] = useState('')
  const [lookupId, setLookupId] = useState('')
  const deferredFilter = useDeferredValue(filter)
  const [autoRefresh, setAutoRefresh] = useState(true)
  const [isInitialLoading, setIsInitialLoading] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [isCreating, setIsCreating] = useState(false)
  const [isCancelling, setIsCancelling] = useState(false)
  const [waitingJobId, setWaitingJobId] = useState<string | null>(null)
  const [createForm, setCreateForm] = useState(defaultCreateForm)

  const selectedJob = jobs.find((job) => job.id === selectedJobId) ?? null
  const visibleJobs = jobs.filter((job) => {
    const term = deferredFilter.trim().toLowerCase()

    if (!term) {
      return true
    }

    return job.id.toLowerCase().includes(term) || job.status.toLowerCase().includes(term)
  })

  const runningCount = jobs.filter((job) => job.status === 'Running').length
  const queuedCount = jobs.filter((job) => job.status === 'Queued').length
  const terminalCount = jobs.filter((job) => isTerminalStatus(job.status)).length

  function applyJobs(nextJobs: Job[], preferredJobId?: string | null) {
    const sortedJobs = sortJobs(nextJobs)

    startTransition(() => {
      setJobs(sortedJobs)
      setSelectedJobId((current) => {
        if (preferredJobId && sortedJobs.some((job) => job.id === preferredJobId)) {
          return preferredJobId
        }

        if (current && sortedJobs.some((job) => job.id === current)) {
          return current
        }

        return sortedJobs[0]?.id ?? null
      })
    })
  }

  function upsertJob(nextJob: Job, shouldSelect = true) {
    startTransition(() => {
      setJobs((currentJobs) => sortJobs([nextJob, ...currentJobs.filter((job) => job.id !== nextJob.id)]))

      if (shouldSelect) {
        setSelectedJobId(nextJob.id)
      }
    })
  }

  async function loadJobs(options?: { silent?: boolean; preferredJobId?: string | null }) {
    const silent = options?.silent ?? false

    if (!silent) {
      setIsRefreshing(true)
    }

    try {
      const nextJobs = await jobsApi.list()
      applyJobs(nextJobs, options?.preferredJobId)
    } catch (error) {
      if (!silent || jobs.length === 0) {
        setNotice({
          tone: 'error',
          title: 'Не удалось получить список задач',
          message: getErrorMessage(error),
        })
      }
    } finally {
      setIsInitialLoading(false)

      if (!silent) {
        setIsRefreshing(false)
      }
    }
  }

  useEffect(() => {
    void loadJobs()
  }, [])

  const pollJobs = useEffectEvent(async () => {
    if (document.hidden) {
      return
    }

    await loadJobs({ silent: true })
  })

  useEffect(() => {
    if (!autoRefresh) {
      return
    }

    const intervalId = window.setInterval(() => {
      void pollJobs()
    }, 3000)

    return () => window.clearInterval(intervalId)
  }, [autoRefresh])

  async function handleCreateJob(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const payload: CreateJobPayload = {
      duration: createForm.duration.trim(),
      shouldFail: createForm.shouldFail,
      timeout: createForm.timeout.trim() ? createForm.timeout.trim() : null,
    }

    setIsCreating(true)
    setNotice(null)

    try {
      const createdJob = await jobsApi.create(payload)
      upsertJob(createdJob)
      setNotice({
        tone: 'success',
        title: 'Задача создана',
        message: `ID: ${createdJob.id}`,
      })
    } catch (error) {
      setNotice({
        tone: 'error',
        title: 'Создать задачу не удалось',
        message: getErrorMessage(error),
      })
    } finally {
      setIsCreating(false)
    }
  }

  async function handleRefreshSelectedJob() {
    if (!selectedJobId) {
      setNotice({
        tone: 'info',
        title: 'Задача не выбрана',
        message: 'Выберите задачу в списке или найдите её по UUID.',
      })
      return
    }

    setIsRefreshing(true)

    try {
      const job = await jobsApi.get(selectedJobId)
      upsertJob(job)
      setNotice({
        tone: 'info',
        title: 'Карточка обновлена',
        message: `Статус задачи ${shortId(job.id)}: ${job.status}.`,
      })
    } catch (error) {
      setNotice({
        tone: 'error',
        title: 'Не удалось получить задачу',
        message: getErrorMessage(error),
      })
    } finally {
      setIsRefreshing(false)
    }
  }

  async function handleLookupById(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const id = normalizeUuid(lookupId)

    if (!id) {
      setNotice({
        tone: 'info',
        title: 'Нужен UUID задачи',
        message: 'Введите идентификатор и попробуйте ещё раз.',
      })
      return
    }

    setIsRefreshing(true)

    try {
      const job = await jobsApi.get(id)
      upsertJob(job)
      setNotice({
        tone: 'success',
        title: 'Задача найдена',
        message: `Карточка ${shortId(job.id)} загружена и выбрана.`,
      })
      setLookupId('')
    } catch (error) {
      setNotice({
        tone: 'error',
        title: 'Поиск по UUID не удался',
        message: getErrorMessage(error),
      })
    } finally {
      setIsRefreshing(false)
    }
  }

  async function handleWaitForCompletion() {
    if (!selectedJob) {
      return
    }

    setWaitingJobId(selectedJob.id)
    setNotice({
      tone: 'info',
      title: 'Ожидание завершения',
      message: `Запрос удерживается до тех пор, пока задача ${shortId(selectedJob.id)} не перейдёт в терминальный статус.`,
    })

    try {
      const job = await jobsApi.wait(selectedJob.id)
      upsertJob(job)
      setNotice({
        tone: job.status === 'Succeeded' ? 'success' : 'info',
        title: 'Задача завершилась',
        message: `Финальный статус: ${job.status}.`,
      })
    } catch (error) {
      setNotice({
        tone: 'error',
        title: 'Ожидание завершения не удалось',
        message: getErrorMessage(error),
      })
    } finally {
      setWaitingJobId(null)
    }
  }

  async function handleCancelSelectedJob() {
    if (!selectedJob) {
      return
    }

    setIsCancelling(true)

    try {
      await jobsApi.cancel(selectedJob.id)
      setNotice({
        tone: 'info',
        title: 'Отмена отправлена',
        message: `Для задачи ${shortId(selectedJob.id)} отправлен запрос на отмену.`,
      })
      await loadJobs({ preferredJobId: selectedJob.id })
    } catch (error) {
      setNotice({
        tone: 'error',
        title: 'Задачу отменить не удалось',
        message: getErrorMessage(error),
      })
    } finally {
      setIsCancelling(false)
    }
  }

  return (
    <div className="min-h-screen text-slate-900">
      <div className="mx-auto flex min-h-screen max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
        <header className="grid gap-6 lg:grid-cols-[minmax(0,1.25fr)_minmax(280px,0.75fr)]">
          <section className="overflow-hidden rounded-[2rem] border border-white/60 bg-white/70 p-6 shadow-[0_24px_70px_-32px_rgba(15,23,42,0.6)] backdrop-blur xl:p-8">
            <span className="inline-flex rounded-full bg-slate-900 px-3 py-1 text-xs font-semibold uppercase tracking-[0.24em] text-white">
              Async Job Scheduler
            </span>
            <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,1fr)_280px] lg:items-end">
              <div>
                <h1 className="max-w-2xl text-4xl font-semibold tracking-tight text-slate-950 sm:text-5xl">
                  Простая панель управления асинхронными задачами.
                </h1>
                <p className="mt-4 max-w-2xl text-base leading-7 text-slate-600 sm:text-lg">
                  Интерфейс покрывает все доступные эндпоинты бэкенда: создание, список, поиск по ID,
                  ожидание завершения и отмену задач.
                </p>
              </div>
              <div className="rounded-[1.75rem] border border-amber-200/80 bg-linear-to-br from-amber-100 via-orange-50 to-cyan-100 p-5">
                <div className="text-sm font-medium uppercase tracking-[0.2em] text-slate-700">Маршруты API</div>
                <ul className="mt-4 space-y-2 text-sm text-slate-700">
                  <li><code>POST /api/jobs</code></li>
                  <li><code>GET /api/jobs</code></li>
                  <li><code>GET /api/jobs/{'{'}id{'}'}</code></li>
                  <li><code>GET /api/jobs/{'{'}id{'}'}/wait</code></li>
                  <li><code>DELETE /api/jobs/{'{'}id{'}'}</code></li>
                </ul>
              </div>
            </div>
          </section>

          <section className="grid gap-4 sm:grid-cols-3 lg:grid-cols-1">
            <MetricCard label="Всего задач" value={jobs.length} accent="bg-slate-900 text-white" />
            <MetricCard label="В работе" value={runningCount + queuedCount} accent="bg-cyan-200 text-cyan-950" />
            <MetricCard label="Завершено" value={terminalCount} accent="bg-emerald-200 text-emerald-950" />
          </section>
        </header>

        {notice ? (
          <section
            className={`rounded-[1.5rem] border px-4 py-4 shadow-[0_18px_36px_-30px_rgba(15,23,42,0.6)] sm:px-5 ${
              notice.tone === 'error'
                ? 'border-rose-200 bg-rose-50 text-rose-950'
                : notice.tone === 'success'
                  ? 'border-emerald-200 bg-emerald-50 text-emerald-950'
                  : 'border-cyan-200 bg-cyan-50 text-cyan-950'
            }`}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 className="text-base font-semibold">{notice.title}</h2>
                <p className="mt-1 text-sm leading-6 opacity-90">{notice.message}</p>
              </div>
              <button
                type="button"
                onClick={() => setNotice(null)}
                className="rounded-full border border-current/15 px-3 py-1 text-xs font-medium uppercase tracking-[0.16em]"
              >
                Скрыть
              </button>
            </div>
          </section>
        ) : null}

        <main className="grid flex-1 gap-6 xl:grid-cols-[360px_minmax(0,1fr)]">
          <section className="rounded-[2rem] border border-white/60 bg-white/70 p-6 shadow-[0_24px_70px_-32px_rgba(15,23,42,0.6)] backdrop-blur">
            <div className="flex items-start justify-between gap-3">
              <div>
                <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">
                  Создание задачи
                </span>
                <h2 className="mt-2 text-2xl font-semibold tracking-tight text-slate-950">
                  Новый job request
                </h2>
              </div>
              <div className="rounded-full bg-slate-900 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-white">
                Form
              </div>
            </div>

            <form className="mt-6 space-y-5" onSubmit={handleCreateJob}>
              <label className="block">
                <span className="mb-2 block text-sm font-medium text-slate-700">Duration</span>
                <input
                  value={createForm.duration}
                  onChange={(event) => {
                    setCreateForm((current) => ({ ...current, duration: event.target.value }))
                  }}
                  placeholder="00:00:15"
                  className="w-full rounded-2xl border border-slate-200 bg-stone-50 px-4 py-3 text-sm outline-none transition focus:border-cyan-500 focus:bg-white"
                />
              </label>

              <label className="block">
                <span className="mb-2 block text-sm font-medium text-slate-700">Timeout</span>
                <input
                  value={createForm.timeout}
                  onChange={(event) => {
                    setCreateForm((current) => ({ ...current, timeout: event.target.value }))
                  }}
                  placeholder="00:00:20"
                  className="w-full rounded-2xl border border-slate-200 bg-stone-50 px-4 py-3 text-sm outline-none transition focus:border-cyan-500 focus:bg-white"
                />
                <span className="mt-2 block text-xs leading-5 text-slate-500">
                  Пустое значение отключает таймаут.
                </span>
              </label>

              <label className="flex items-center gap-3 rounded-2xl border border-slate-200 bg-stone-50 px-4 py-3 text-sm text-slate-700">
                <input
                  type="checkbox"
                  checked={createForm.shouldFail}
                  onChange={(event) => {
                    setCreateForm((current) => ({ ...current, shouldFail: event.target.checked }))
                  }}
                  className="size-4 rounded border-slate-300 text-rose-500 accent-rose-500"
                />
                <span>Симулировать ошибку выполнения</span>
              </label>

              <button
                type="submit"
                disabled={isCreating}
                className="inline-flex w-full items-center justify-center rounded-2xl bg-slate-900 px-4 py-3 text-sm font-semibold text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-500"
              >
                {isCreating ? 'Создание...' : 'Создать задачу'}
              </button>
            </form>

            <div className="mt-6 rounded-[1.5rem] border border-slate-200 bg-stone-50 p-4">
              <h3 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Подсказки</h3>
              <ul className="mt-3 space-y-2 text-sm leading-6 text-slate-600">
                <li>Используйте формат <code>hh:mm:ss</code>, например <code>00:00:10</code>.</li>
                <li><code>shouldFail=true</code> удобно для проверки статуса <code>Failed</code>.</li>
                <li>После создания задача автоматически попадает в общий список.</li>
              </ul>
            </div>
          </section>

          <section className="grid gap-6">
            <div className="rounded-[2rem] border border-white/60 bg-white/70 p-5 shadow-[0_24px_70px_-32px_rgba(15,23,42,0.6)] backdrop-blur">
              <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
                <div>
                  <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Контроль</span>
                  <h2 className="mt-2 text-2xl font-semibold tracking-tight text-slate-950">Список и действия</h2>
                </div>

                <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
                  <input
                    value={filter}
                    onChange={(event) => setFilter(event.target.value)}
                    placeholder="Фильтр по ID или статусу"
                    className="w-full rounded-2xl border border-slate-200 bg-stone-50 px-4 py-3 text-sm outline-none transition focus:border-cyan-500 focus:bg-white sm:min-w-72"
                  />
                  <button
                    type="button"
                    onClick={() => void loadJobs()}
                    disabled={isRefreshing}
                    className="rounded-2xl border border-slate-300 px-4 py-3 text-sm font-semibold text-slate-700 transition hover:border-slate-900 hover:text-slate-950 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {isRefreshing ? 'Обновление...' : 'Обновить список'}
                  </button>
                  <label className="inline-flex items-center gap-3 rounded-2xl border border-slate-200 bg-stone-50 px-4 py-3 text-sm text-slate-700">
                    <input
                      type="checkbox"
                      checked={autoRefresh}
                      onChange={(event) => setAutoRefresh(event.target.checked)}
                      className="size-4 rounded border-slate-300 accent-cyan-600"
                    />
                    Автообновление
                  </label>
                </div>
              </div>
            </div>

            <div className="grid gap-6 2xl:grid-cols-[minmax(0,1.05fr)_minmax(340px,0.95fr)]">
              <section className="rounded-[2rem] border border-white/60 bg-white/70 p-5 shadow-[0_24px_70px_-32px_rgba(15,23,42,0.6)] backdrop-blur">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <h3 className="text-xl font-semibold tracking-tight text-slate-950">Очередь задач</h3>
                    <p className="mt-1 text-sm text-slate-500">
                      Выберите задачу, чтобы посмотреть детали и действия.
                    </p>
                  </div>
                  <span className="rounded-full bg-slate-900 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-white">
                    {visibleJobs.length} visible
                  </span>
                </div>

                <div className="mt-5 space-y-3">
                  {isInitialLoading ? (
                    <div className="rounded-[1.5rem] border border-dashed border-slate-300 bg-stone-50 px-4 py-10 text-center text-sm text-slate-500">
                      Загружаю текущие задачи...
                    </div>
                  ) : visibleJobs.length === 0 ? (
                    <div className="rounded-[1.5rem] border border-dashed border-slate-300 bg-stone-50 px-4 py-10 text-center text-sm text-slate-500">
                      Список пуст. Создайте новую задачу слева или снимите фильтр.
                    </div>
                  ) : (
                    <div className="max-h-[40rem] space-y-3 overflow-auto pr-1">
                      {visibleJobs.map((job) => {
                        const progressWidth = `${Math.max(0, Math.min(100, job.progress * 100))}%`
                        const selected = job.id === selectedJobId

                        return (
                          <button
                            key={job.id}
                            type="button"
                            onClick={() => setSelectedJobId(job.id)}
                            className={`block w-full rounded-[1.5rem] border p-4 text-left transition ${
                              selected
                                ? 'border-slate-900 bg-slate-900 text-white shadow-[0_24px_40px_-28px_rgba(15,23,42,0.95)]'
                                : 'border-slate-200 bg-stone-50 hover:border-cyan-400 hover:bg-white'
                            }`}
                          >
                            <div className="flex flex-wrap items-center justify-between gap-3">
                              <div>
                                <div className="text-xs font-semibold uppercase tracking-[0.22em] opacity-70">
                                  {selected ? 'Selected' : 'Job'}
                                </div>
                                <div className="mt-1 text-base font-semibold tracking-tight">{shortId(job.id)}</div>
                              </div>
                              <StatusBadge status={job.status} inverted={selected} />
                            </div>

                            <div className="mt-4 space-y-3">
                              <div className="flex items-center justify-between text-xs uppercase tracking-[0.18em] opacity-70">
                                <span>Progress</span>
                                <span>{formatPercent(job.progress)}</span>
                              </div>
                              <div className={`h-2 rounded-full ${selected ? 'bg-white/20' : 'bg-slate-200'}`}>
                                <div
                                  className={`h-2 rounded-full ${selected ? 'bg-white' : 'bg-slate-900'}`}
                                  style={{ width: progressWidth }}
                                />
                              </div>
                              <div className="grid gap-2 text-sm sm:grid-cols-2">
                                <Field label="Создана" value={formatDate(job.createdAt)} compact />
                                <Field label="Завершена" value={formatDate(job.finishedAt)} compact />
                              </div>
                            </div>
                          </button>
                        )
                      })}
                    </div>
                  )}
                </div>
              </section>

              <section className="rounded-[2rem] border border-white/60 bg-white/70 p-5 shadow-[0_24px_70px_-32px_rgba(15,23,42,0.6)] backdrop-blur">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <h3 className="text-xl font-semibold tracking-tight text-slate-950">Детали задачи</h3>
                    <p className="mt-1 text-sm text-slate-500">
                      Здесь работают <code>GET /api/jobs/{'{'}id{'}'}</code>, <code>GET /wait</code> и <code>DELETE</code>.
                    </p>
                  </div>
                  {selectedJob ? <StatusBadge status={selectedJob.status} /> : null}
                </div>

                <form className="mt-5 flex flex-col gap-3 sm:flex-row" onSubmit={handleLookupById}>
                  <input
                    value={lookupId}
                    onChange={(event) => setLookupId(event.target.value)}
                    placeholder="UUID задачи"
                    className="min-w-0 flex-1 rounded-2xl border border-slate-200 bg-stone-50 px-4 py-3 text-sm outline-none transition focus:border-cyan-500 focus:bg-white"
                  />
                  <button
                    type="submit"
                    disabled={isRefreshing}
                    className="rounded-2xl border border-slate-300 px-4 py-3 text-sm font-semibold text-slate-700 transition hover:border-slate-900 hover:text-slate-950 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    Найти по ID
                  </button>
                </form>

                {selectedJob ? (
                  <div className="mt-6 space-y-5">
                    <div className="rounded-[1.5rem] border border-slate-200 bg-stone-50 p-4">
                      <div className="flex flex-col gap-4">
                        <Field label="Полный ID" value={selectedJob.id} />
                        <Field label="Статус" value={selectedJob.status} />
                        <Field label="Прогресс" value={formatPercent(selectedJob.progress)} />
                        <Field label="Создана" value={formatDate(selectedJob.createdAt)} />
                        <Field label="Стартовала" value={formatDate(selectedJob.startedAt)} />
                        <Field label="Завершена" value={formatDate(selectedJob.finishedAt)} />
                        <Field label="Ошибка" value={selectedJob.error ?? '—'} />
                      </div>

                      <div className="mt-4">
                        <div className="mb-2 flex items-center justify-between text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
                          <span>Progress bar</span>
                          <span>{formatPercent(selectedJob.progress)}</span>
                        </div>
                        <div className="h-3 rounded-full bg-slate-200">
                          <div
                            className="h-3 rounded-full bg-linear-to-r from-cyan-500 via-slate-900 to-amber-500"
                            style={{ width: `${Math.max(0, Math.min(100, selectedJob.progress * 100))}%` }}
                          />
                        </div>
                      </div>
                    </div>

                    <div className="grid gap-3 sm:grid-cols-3">
                      <button
                        type="button"
                        onClick={() => void handleRefreshSelectedJob()}
                        disabled={isRefreshing}
                        className="rounded-2xl border border-slate-300 px-4 py-3 text-sm font-semibold text-slate-700 transition hover:border-slate-900 hover:text-slate-950 disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        Обновить
                      </button>
                      <button
                        type="button"
                        onClick={() => void handleWaitForCompletion()}
                        disabled={Boolean(waitingJobId) || isTerminalStatus(selectedJob.status)}
                        className="rounded-2xl bg-cyan-600 px-4 py-3 text-sm font-semibold text-white transition hover:bg-cyan-700 disabled:cursor-not-allowed disabled:bg-cyan-300"
                      >
                        {waitingJobId === selectedJob.id ? 'Ожидание...' : 'Ждать завершения'}
                      </button>
                      <button
                        type="button"
                        onClick={() => void handleCancelSelectedJob()}
                        disabled={isCancelling || isTerminalStatus(selectedJob.status)}
                        className="rounded-2xl bg-rose-600 px-4 py-3 text-sm font-semibold text-white transition hover:bg-rose-700 disabled:cursor-not-allowed disabled:bg-rose-300"
                      >
                        {isCancelling ? 'Отмена...' : 'Отменить'}
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="mt-6 rounded-[1.5rem] border border-dashed border-slate-300 bg-stone-50 px-4 py-10 text-center text-sm text-slate-500">
                    Выберите задачу в списке или загрузите её по UUID.
                  </div>
                )}
              </section>
            </div>
          </section>
        </main>
      </div>
    </div>
  )
}

function MetricCard(props: { label: string; value: number; accent: string }) {
  return (
    <article className="rounded-[1.75rem] border border-white/60 bg-white/70 p-5 shadow-[0_24px_70px_-32px_rgba(15,23,42,0.6)] backdrop-blur">
      <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">{props.label}</div>
      <div
        className={`mt-4 inline-flex rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] ${props.accent}`}
      >
        Live
      </div>
      <div className="mt-4 text-4xl font-semibold tracking-tight text-slate-950">{props.value}</div>
    </article>
  )
}

function Field(props: { label: string; value: string; compact?: boolean }) {
  return (
    <div className={props.compact ? 'space-y-1' : 'rounded-2xl border border-white bg-white/70 p-3'}>
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{props.label}</div>
      <div className="break-all text-sm leading-6 text-slate-700">{props.value}</div>
    </div>
  )
}

function StatusBadge(props: { status: Job['status']; inverted?: boolean }) {
  return (
    <span
      className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] ${
        props.inverted ? 'bg-white/15 text-white ring-1 ring-white/20' : statusStyles[props.status]
      }`}
    >
      {props.status}
    </span>
  )
}
