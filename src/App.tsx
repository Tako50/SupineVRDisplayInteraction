import { useCallback, useEffect, useMemo, useState } from 'react'
import itemsData from './data/items.json'
import practiceItemsData from './data/practiceItems.json'
import CandidateList from './components/CandidateList'
import CategoryList from './components/CategoryList'
import ConfirmedCandidate from './components/ConfirmedCandidate'
import Home from './components/Home'
import ProductDetail from './components/ProductDetail'
import ProductList from './components/ProductList'
import { logEvent } from './utils/logger'
import type { Category, Item } from './types'

const isPracticeMode = new URLSearchParams(window.location.search).get('mode') === 'practice'
const CANDIDATES_STORAGE_KEY = isPracticeMode
  ? 't2_practice_web_candidates'
  : 't2_web_candidates'

const CATEGORY_DISPLAY_ORDER: Record<Category, string[]> = {
  '居住・寝具': ['A04', 'A08', 'A09', 'B01', 'B04'],
  '収納・ゴミ箱': ['A01', 'A02', 'A05', 'A06', 'B02', 'B03'],
  '照明・ガジェット': ['A03', 'B06', 'B07', 'B10'],
  '火器・調理用品': ['A07', 'B05', 'B08', 'B09'],
  'ファニチャー': ['A10'],
}

const displayOrderById = new Map(
  Object.values(CATEGORY_DISPLAY_ORDER)
    .flat()
    .map((itemId, index) => [itemId, index] as const),
)

const activeItemsData = isPracticeMode ? practiceItemsData : itemsData
const items = (activeItemsData as Item[])
  .filter((item) => item.display_in_web)
  .sort((left, right) =>
    (displayOrderById.get(left.item_id) ?? Number.MAX_SAFE_INTEGER)
    - (displayOrderById.get(right.item_id) ?? Number.MAX_SAFE_INTEGER)
    || left.item_order - right.item_order,
  )

type View =
  | { name: 'home' }
  | { name: 'categories' }
  | { name: 'products'; category: Category }
  | { name: 'detail'; category: Category; itemId: string }
  | { name: 'candidates' }
  | { name: 'confirmed'; itemId: string }

const HISTORY_VIEW_KEY = '__t2View'
const HISTORY_DEPTH_KEY = '__t2Depth'

function readHistoryView(): View | null {
  const candidate = window.history.state?.[HISTORY_VIEW_KEY] as Partial<View> | undefined
  if (!candidate || typeof candidate.name !== 'string') return null

  switch (candidate.name) {
    case 'home':
    case 'categories':
    case 'candidates':
      return { name: candidate.name }
    case 'products':
      return typeof candidate.category === 'string'
        && Object.hasOwn(CATEGORY_DISPLAY_ORDER, candidate.category)
        ? { name: 'products', category: candidate.category as Category }
        : null
    case 'detail':
      return typeof candidate.category === 'string'
        && Object.hasOwn(CATEGORY_DISPLAY_ORDER, candidate.category)
        && typeof candidate.itemId === 'string'
        ? { name: 'detail', category: candidate.category as Category, itemId: candidate.itemId }
        : null
    case 'confirmed':
      return typeof candidate.itemId === 'string'
        ? { name: 'confirmed', itemId: candidate.itemId }
        : null
    default:
      return null
  }
}

function readHistoryDepth(): number {
  const depth = Number(window.history.state?.[HISTORY_DEPTH_KEY])
  return Number.isInteger(depth) && depth >= 0 ? depth : 0
}

function createHistoryState(view: View, depth: number) {
  return {
    ...window.history.state,
    [HISTORY_VIEW_KEY]: view,
    [HISTORY_DEPTH_KEY]: Math.max(0, depth),
  }
}

declare global {
  interface Window {
    t2ResetCandidates?: () => void
    t2ConfirmCandidate?: (itemId: string) => void
    t2RejectCandidate?: (message: string) => void
    t2SetConfirmationLock?: (locked: boolean, remainingSeconds: number) => void
    t2OpenCandidateList?: () => void
    __t2OpenCandidateListRequested?: boolean
    __t2UnitySend?: (type: string, value: string) => void
  }
}

function loadCandidateIds(): string[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(CANDIDATES_STORAGE_KEY) ?? '[]')
    return Array.isArray(parsed)
      ? parsed.filter((id): id is string => typeof id === 'string' && items.some((item) => item.item_id === id))
      : []
  } catch {
    return []
  }
}

export default function App() {
  const [view, setView] = useState<View>(() => readHistoryView() ?? { name: 'home' })
  const [candidateIds, setCandidateIds] = useState<string[]>(loadCandidateIds)
  const [pendingConfirmationId, setPendingConfirmationId] = useState<string | null>(null)
  const [confirmationError, setConfirmationError] = useState('')
  const [confirmationLocked, setConfirmationLocked] = useState(false)
  const [confirmationUnlockRemainingSeconds, setConfirmationUnlockRemainingSeconds] = useState(0)

  const navigateTo = useCallback((nextView: View, replace = false) => {
    const nextDepth = replace ? readHistoryDepth() : readHistoryDepth() + 1
    const nextState = createHistoryState(nextView, nextDepth)
    if (replace) {
      window.history.replaceState(nextState, '')
    } else {
      window.history.pushState(nextState, '')
    }
    setView(nextView)
  }, [])

  useEffect(() => {
    const storedView = readHistoryView()
    const initialView = storedView ?? { name: 'home' }
    if (!storedView) {
      window.history.replaceState(createHistoryState(initialView, 0), '')
    }

    const handlePopState = () => {
      setView(readHistoryView() ?? { name: 'home' })
    }
    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  useEffect(() => {
    try {
      localStorage.setItem(CANDIDATES_STORAGE_KEY, JSON.stringify(candidateIds))
    } catch (error) {
      console.warn('候補リストを保存できませんでした。', error)
    }
  }, [candidateIds])

  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'auto' })
  }, [view])

  useEffect(() => {
    window.t2ResetCandidates = () => {
      try {
        localStorage.setItem(CANDIDATES_STORAGE_KEY, '[]')
      } catch (error) {
        console.warn('候補リストを消去できませんでした。', error)
      }
      setCandidateIds([])
      setPendingConfirmationId(null)
      setConfirmationError('')
      navigateTo({ name: 'home' }, true)
    }
    window.t2ConfirmCandidate = (itemId) => {
      const item = items.find((candidate) => candidate.item_id === itemId)
      setPendingConfirmationId(null)
      if (!item) {
        setConfirmationError('確定した商品を表示できませんでした。実験者にお知らせください。')
        navigateTo({ name: 'candidates' }, true)
        return
      }

      setConfirmationError('')
      navigateTo({ name: 'confirmed', itemId: item.item_id }, true)
    }
    window.t2RejectCandidate = (message) => {
      setPendingConfirmationId(null)
      setConfirmationError(message || '条件を確認して、もう一度選択してください。')
      navigateTo({ name: 'candidates' }, true)
    }
    window.t2SetConfirmationLock = (locked, remainingSeconds) => {
      setConfirmationLocked(Boolean(locked))
      setConfirmationUnlockRemainingSeconds(Math.max(0, Math.ceil(Number(remainingSeconds) || 0)))
      if (locked) {
        setPendingConfirmationId(null)
      }
    }
    window.t2OpenCandidateList = () => {
      window.__t2OpenCandidateListRequested = false
      setPendingConfirmationId(null)
      setConfirmationError('')
      navigateTo({ name: 'candidates' })
      logEvent('candidate_list_open', { from: 'confirmation_unlock' })
    }
    if (window.__t2OpenCandidateListRequested) {
      window.t2OpenCandidateList()
    }
    return () => {
      delete window.t2ResetCandidates
      delete window.t2ConfirmCandidate
      delete window.t2RejectCandidate
      delete window.t2SetConfirmationLock
      delete window.t2OpenCandidateList
    }
  }, [navigateTo])

  const candidates = useMemo(
    () => candidateIds.flatMap((id) => {
      const item = items.find((candidate) => candidate.item_id === id)
      return item ? [item] : []
    }),
    [candidateIds],
  )

  const goBack = (steps: number, fallback: View, from: string) => {
    logEvent('back', { from, to: fallback.name })
    if (readHistoryDepth() >= steps) {
      window.history.go(-steps)
      return
    }
    navigateTo(fallback, true)
  }

  const goHome = (from: string) => {
    const depth = readHistoryDepth()
    logEvent('back', { from, to: 'home' })
    if (depth > 0) {
      window.history.go(-depth)
      return
    }
    navigateTo({ name: 'home' }, true)
  }

  const openCandidates = (from: string) => {
    logEvent('candidate_list_open', { from })
    navigateTo({ name: 'candidates' })
  }

  const currentItem = view.name === 'detail'
    ? items.find((item) => item.item_id === view.itemId)
    : undefined
  const confirmedItem = view.name === 'confirmed'
    ? items.find((item) => item.item_id === view.itemId)
    : undefined

  return (
    <div className="app-shell">
      {view.name === 'home' && (
        <Home
          isPracticeMode={isPracticeMode}
          candidateCount={candidateIds.length}
          onOpenCategories={() => navigateTo({ name: 'categories' })}
          onOpenCandidates={() => openCandidates('home')}
        />
      )}

      {view.name === 'categories' && (
        <CategoryList
          items={items}
          onSelect={(category) => {
            logEvent('category_open', { category })
            navigateTo({ name: 'products', category })
          }}
          onBack={() => goHome('categories')}
        />
      )}

      {view.name === 'products' && (
        <ProductList
          category={view.category}
          items={items.filter((item) => item.category === view.category)}
          candidateCount={candidateIds.length}
          onOpenProduct={(item) => {
            logEvent('product_open', { item_id: item.item_id })
            navigateTo({ name: 'detail', category: view.category, itemId: item.item_id })
          }}
          onOpenCandidates={() => openCandidates('products')}
          onBack={() => goBack(1, { name: 'categories' }, 'products')}
        />
      )}

      {view.name === 'detail' && currentItem && (
        <ProductDetail
          item={currentItem}
          isCandidate={candidateIds.includes(currentItem.item_id)}
          candidateCount={candidateIds.length}
          onAddCandidate={(item) => {
            if (candidateIds.includes(item.item_id)) return
            setCandidateIds((current) => [...current, item.item_id])
            setPendingConfirmationId(null)
            setConfirmationError('')
            logEvent('add_candidate', { item_id: item.item_id })
          }}
          onBackToProducts={() => goBack(1, { name: 'products', category: view.category }, 'detail')}
          onBackToCategories={() => goBack(2, { name: 'categories' }, 'detail')}
          onBackHome={() => goHome('detail')}
          onOpenCandidates={() => openCandidates('detail')}
          onOpenProductPage={(item) => {
            logEvent('product_page_open', { item_id: item.item_id, url: item.product_url })
          }}
        />
      )}

      {view.name === 'detail' && !currentItem && (
        <main className="screen empty-state">
          <h1>商品が見つかりません</h1>
          <button className="primary-button" onClick={() => goBack(1, { name: 'categories' }, 'detail_missing')}>
            カテゴリ一覧に戻る
          </button>
        </main>
      )}

      {view.name === 'candidates' && (
        <CandidateList
          candidates={candidates}
          pendingConfirmationId={pendingConfirmationId}
          confirmationError={confirmationError}
          confirmationLocked={confirmationLocked}
          confirmationUnlockRemainingSeconds={confirmationUnlockRemainingSeconds}
          onRemove={(item) => {
            setCandidateIds((current) => current.filter((id) => id !== item.item_id))
            setPendingConfirmationId(null)
            setConfirmationError('')
            logEvent('remove_candidate', { item_id: item.item_id })
          }}
          onConfirm={(item) => {
            if (confirmationLocked) return
            setPendingConfirmationId(item.item_id)
            setConfirmationError('')
            logEvent('confirm_candidates', { item_id: item.item_id, item_ids: [item.item_id] })
            if (typeof window.__t2UnitySend !== 'function') {
              window.t2ConfirmCandidate?.(item.item_id)
            }
          }}
          onBack={() => goHome('candidates')}
        />
      )}

      {view.name === 'confirmed' && confirmedItem && (
        <ConfirmedCandidate
          item={confirmedItem}
          onResetAndReturnHome={() => {
            try {
              localStorage.setItem(CANDIDATES_STORAGE_KEY, '[]')
            } catch (error) {
              console.warn('候補リストを消去できませんでした。', error)
            }
            setCandidateIds([])
            setPendingConfirmationId(null)
            setConfirmationError('')
            logEvent('reset_candidates_and_home', { item_id: confirmedItem.item_id })
            navigateTo({ name: 'home' }, true)
            window.__t2UnitySend?.('action', 'reset_candidates_and_home')
          }}
        />
      )}

      {view.name === 'confirmed' && !confirmedItem && (
        <main className="screen empty-state">
          <h1>確定した商品を表示できません</h1>
          <button
            className="primary-button"
            onClick={() => {
              window.t2ResetCandidates?.()
              window.__t2UnitySend?.('action', 'reset_candidates_and_home')
            }}
          >
            候補を消去してホームに戻る
          </button>
        </main>
      )}

    </div>
  )
}
