import type { Item } from '../types'
import ProductImage from './ProductImage'

interface ProductDetailProps {
  item: Item
  isCandidate: boolean
  candidateLimitReached: boolean
  onAddCandidate: (item: Item) => void
  onBack: () => void
}

export default function ProductDetail({
  item,
  isCandidate,
  candidateLimitReached,
  onAddCandidate,
  onBack,
}: ProductDetailProps) {
  return (
    <main className="screen detail-screen" aria-labelledby="detail-title">
      <button className="back-button" onClick={onBack}>← 商品一覧に戻る</button>
      <div className="detail-layout">
        <ProductImage item={item} className="detail-image" />
        <section className="detail-content">
          <h1 id="detail-title">{item.product_name}</h1>
          <p className="brand detail-brand">{item.brand}</p>
          <p className="detail-description">{item.web_description}</p>
          <dl className="spec-list">
            <div><dt>カテゴリ</dt><dd>{item.category}</dd></div>
            <div><dt>サイズ</dt><dd>{item.size}</dd></div>
            <div><dt>重量</dt><dd>{item.weight}</dd></div>
            <div><dt>価格</dt><dd>{item.price}</dd></div>
            <div><dt>素材</dt><dd>{item.material}</dd></div>
            <div className="visual-feature"><dt>動画内での見た目・特徴</dt><dd>{item.visual_features}</dd></div>
          </dl>
          <div className="detail-actions">
            <button
              className="primary-button"
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
            <button className="secondary-button" onClick={onBack}>商品一覧に戻る</button>
          </div>
        </section>
      </div>
    </main>
  )
}
