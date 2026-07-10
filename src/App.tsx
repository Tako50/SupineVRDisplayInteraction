import { useEffect, useMemo, useState } from 'react'
import itemsData from './data/items.json'
import CandidateList from './components/CandidateList'
import CategoryList from './components/CategoryList'
import Home from './components/Home'
import ProductDetail from './components/ProductDetail'
import ProductList from './components/ProductList'
import { logEvent } from './utils/logger'
import type { Category, Item } from './types'

const CANDIDATES_STORAGE_KEY = 't2_web_candidates'

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

const items = (itemsData as Item[])
  .filter((item) => item.display_in_web)
  .sort((left, right) =>
    (displayOrderById.get(left.item_id) ?? Number.MAX_SAFE_INTEGER)
    - (displayOrderById.get(right.item_id) ?? Number.MAX_SAFE_INTEGER),
  )

type View =
  | { name: 'home' }
  | { name: 'categories' }
  | { name: 'products'; category: Category }
  | { name: 'detail'; category: Category; itemId: string }
  | { name: 'candidates' }

declare global {
  interface Window {
    t2ResetCandidates?: () => void
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
  const [view, setView] = useState<View>({ name: 'home' })
  const [candidateIds, setCandidateIds] = useState<string[]>(loadCandidateIds)
  const [confirmedIds, setConfirmedIds] = useState<string[] | null>(null)

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
      setCandidateIds([])
      setConfirmedIds(null)
      setView({ name: 'home' })
    }
    return () => {
      delete window.t2ResetCandidates
    }
  }, [])

  const candidates = useMemo(
    () => candidateIds.flatMap((id) => {
      const item = items.find((candidate) => candidate.item_id === id)
      return item ? [item] : []
    }),
    [candidateIds],
  )

  const goBack = (to: View, from: string) => {
    logEvent('back', { from, to: to.name })
    setView(to)
  }

  const openCandidates = (from: string) => {
    logEvent('candidate_list_open', { from })
    setView({ name: 'candidates' })
  }

  const currentItem = view.name === 'detail'
    ? items.find((item) => item.item_id === view.itemId)
    : undefined

  return (
    <div className="app-shell">
      {view.name === 'home' && (
        <Home
          candidateCount={candidateIds.length}
          onOpenCategories={() => setView({ name: 'categories' })}
          onOpenCandidates={() => openCandidates('home')}
        />
      )}

      {view.name === 'categories' && (
        <CategoryList
          items={items}
          onSelect={(category) => {
            logEvent('category_open', { category })
            setView({ name: 'products', category })
          }}
          onBack={() => goBack({ name: 'home' }, 'categories')}
        />
      )}

      {view.name === 'products' && (
        <ProductList
          category={view.category}
          items={items.filter((item) => item.category === view.category)}
          candidateCount={candidateIds.length}
          onOpenProduct={(item) => {
            logEvent('product_open', { item_id: item.item_id })
            setView({ name: 'detail', category: view.category, itemId: item.item_id })
          }}
          onOpenCandidates={() => openCandidates('products')}
          onBack={() => goBack({ name: 'categories' }, 'products')}
        />
      )}

      {view.name === 'detail' && currentItem && (
        <ProductDetail
          item={currentItem}
          isCandidate={candidateIds.includes(currentItem.item_id)}
          candidateLimitReached={candidateIds.length >= 3}
          candidateCount={candidateIds.length}
          onAddCandidate={(item) => {
            if (candidateIds.includes(item.item_id) || candidateIds.length >= 3) return
            setCandidateIds((current) => [...current, item.item_id])
            setConfirmedIds(null)
            logEvent('add_candidate', { item_id: item.item_id })
          }}
          onBackToProducts={() => goBack({ name: 'products', category: view.category }, 'detail')}
          onBackToCategories={() => goBack({ name: 'categories' }, 'detail')}
          onBackHome={() => goBack({ name: 'home' }, 'detail')}
          onOpenCandidates={() => openCandidates('detail')}
        />
      )}

      {view.name === 'detail' && !currentItem && (
        <main className="screen empty-state">
          <h1>商品が見つかりません</h1>
          <button className="primary-button" onClick={() => goBack({ name: 'categories' }, 'detail_missing')}>
            カテゴリ一覧に戻る
          </button>
        </main>
      )}

      {view.name === 'candidates' && (
        <CandidateList
          candidates={candidates}
          confirmedIds={confirmedIds}
          onRemove={(item) => {
            setCandidateIds((current) => current.filter((id) => id !== item.item_id))
            setConfirmedIds(null)
            logEvent('remove_candidate', { item_id: item.item_id })
          }}
          onConfirm={(item) => {
            setConfirmedIds([item.item_id])
            logEvent('confirm_candidates', { item_id: item.item_id, item_ids: [item.item_id] })
          }}
          onBack={() => goBack({ name: 'home' }, 'candidates')}
        />
      )}

    </div>
  )
}
