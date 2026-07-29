import type { Category, Item } from '../types'
import ProductCard from './ProductCard'

interface ProductListProps {
  category: Category
  items: Item[]
  candidateCount: number
  onOpenProduct: (item: Item) => void
  onOpenCandidates: () => void
  onBack: () => void
}

export default function ProductList({
  category,
  items,
  candidateCount,
  onOpenProduct,
  onOpenCandidates,
  onBack,
}: ProductListProps) {
  return (
    <main className="screen" aria-labelledby="product-list-title">
      <div className="screen-toolbar">
        <button className="back-button" onClick={onBack}>← カテゴリ一覧に戻る</button>
        <button className="back-button" onClick={onOpenCandidates}>
          候補リストを見る
          <span className="count-badge">{candidateCount}</span>
        </button>
      </div>
      <div className="page-heading">
        <p className="eyebrow">カテゴリ内の商品</p>
        <h1 id="product-list-title">{category}</h1>
        <p>このカテゴリには{items.length}件の商品があります。</p>
      </div>
      {items.length > 0 ? (
        <div className="product-grid">
          {items.map((item, index) => (
            <ProductCard
              key={item.item_id}
              item={item}
              orderNumber={index + 1}
              onOpen={onOpenProduct}
            />
          ))}
        </div>
      ) : (
        <div className="empty-state">
          <h2>商品データは準備中です</h2>
          <p>別のカテゴリを選んでください。</p>
          <button className="primary-button" onClick={onBack}>カテゴリ一覧に戻る</button>
        </div>
      )}
    </main>
  )
}
