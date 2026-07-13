import type { Item } from '../types'
import ProductImage from './ProductImage'

interface ProductDetailProps {
  item: Item
  isCandidate: boolean
  candidateLimitReached: boolean
  candidateCount: number
  onAddCandidate: (item: Item) => void
  onBackToProducts: () => void
  onBackToCategories: () => void
  onBackHome: () => void
  onOpenCandidates: () => void
}

export default function ProductDetail({
  item,
  isCandidate,
  candidateLimitReached,
  candidateCount,
  onAddCandidate,
  onBackToProducts,
  onBackToCategories,
  onBackHome,
  onOpenCandidates,
}: ProductDetailProps) {
  return (
    <main className="screen detail-screen" aria-labelledby="detail-title">
      <div className="screen-toolbar">
        <button className="back-button" onClick={onBackToProducts}>← {item.category}の商品に戻る</button>
        <button className="back-button" onClick={onOpenCandidates}>
          候補リストを見る
          <span className="count-badge">{candidateCount}</span>
        </button>
      </div>
      <div className="detail-layout">
        <ProductImage item={item} className="detail-image" />
        <section className="detail-content">
          <h1 id="detail-title">{item.product_name}</h1>
          <p className="brand detail-brand">{item.brand}</p>
          <dl className="spec-list">
            <div><dt>カテゴリ</dt><dd>{item.category}</dd></div>
            <div><dt>サイズ</dt><dd>{item.size}</dd></div>
            <div><dt>重量</dt><dd>{item.weight}</dd></div>
            <div><dt>価格</dt><dd>{item.price}</dd></div>
            <div><dt>素材</dt><dd>{item.material}</dd></div>
            <div className="product-introduction"><dt>商品紹介</dt><dd>{item.web_description}</dd></div>
          </dl>
          <div className="detail-actions">
            <button
              className="primary-button candidate-action"
              data-t2-action={`add_candidate:${item.item_id}`}
              onClick={() => onAddCandidate(item)}
              disabled={isCandidate || candidateLimitReached}
            >
              {isCandidate
                ? '候補に追加済み'
                : candidateLimitReached
                  ? '候補は3個までです'
                  : '候補に追加'}
            </button>
            {isCandidate && (
              <p className="candidate-added-message" aria-live="polite">候補に追加しました。</p>
            )}
            <button className="secondary-button" onClick={onBackToProducts}>同じカテゴリを見る</button>
            <button className="secondary-button" onClick={onBackToCategories}>別のカテゴリを探す</button>
            <button className="secondary-button" onClick={onOpenCandidates}>
              候補リストを見る
              <span className="count-badge">{candidateCount}</span>
            </button>
            <button className="secondary-button" onClick={onBackHome}>ホームに戻る</button>
          </div>
        </section>
      </div>
    </main>
  )
}
