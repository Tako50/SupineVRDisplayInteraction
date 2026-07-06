import { CATEGORIES, type Category, type Item } from '../types'

interface CategoryListProps {
  items: Item[]
  onSelect: (category: Category) => void
  onBack: () => void
}

const descriptions: Record<Category, string> = {
  '居住・寝具': 'テント、寝袋、居住空間の道具',
  '収納・ゴミ箱': 'バッグ、ボックス、整理の道具',
  '照明・ガジェット': 'ランタン、電源、電子機器',
  '調理器具・燃料': 'クッカー、バーナー、燃料用品',
  'ファニチャー': 'テーブル、チェア、ラック',
}

export default function CategoryList({ items, onSelect, onBack }: CategoryListProps) {
  return (
    <main className="screen" aria-labelledby="category-title">
      <button className="back-button" onClick={onBack}>← ホームに戻る</button>
      <div className="page-heading">
        <p className="eyebrow">CATEGORY</p>
        <h1 id="category-title">カテゴリから探す</h1>
        <p>確認したい商品のカテゴリを選んでください。</p>
      </div>
      <div className="category-grid">
        {CATEGORIES.map((category) => {
          const count = items.filter((item) => item.category === category).length
          return (
            <button className="category-card" key={category} onClick={() => onSelect(category)}>
              <span>
                <strong>{category}</strong>
                <small>{descriptions[category]}</small>
              </span>
              <span className="category-count">{count}商品</span>
            </button>
          )
        })}
      </div>
    </main>
  )
}
