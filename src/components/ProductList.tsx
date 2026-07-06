import type { Category, Item } from '../types'
import ProductCard from './ProductCard'

interface ProductListProps {
  category: Category
  items: Item[]
  onOpenProduct: (item: Item) => void
  onBack: () => void
}

export default function ProductList({ category, items, onOpenProduct, onBack }: ProductListProps) {
  return (
    <main className="screen" aria-labelledby="product-list-title">
      <button className="back-button" onClick={onBack}>← カテゴリ一覧に戻る</button>
      <div className="page-heading">
        <p className="eyebrow">PRODUCTS</p>
        <h1 id="product-list-title">{category}</h1>
        <p>{items.length}件の商品があります。</p>
      </div>
      {items.length > 0 ? (
        <div className="product-grid">
          {items.map((item) => (
            <ProductCard key={item.item_id} item={item} onOpen={onOpenProduct} />
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
