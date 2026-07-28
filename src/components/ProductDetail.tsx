import { useState } from 'react'
import type { Item } from '../types'
import { getProductReviews } from '../data/reviewDetails'
import ProductImage from './ProductImage'

interface ProductDetailProps {
  item: Item
  isCandidate: boolean
  candidateCount: number
  onAddCandidate: (item: Item) => void
  onBackToProducts: () => void
  onBackToCategories: () => void
  onBackHome: () => void
  onOpenCandidates: () => void
  onOpenProductPage: (item: Item) => void
}

export default function ProductDetail({
  item,
  isCandidate,
  candidateCount,
  onAddCandidate,
  onBackToProducts,
  onBackToCategories,
  onBackHome,
  onOpenCandidates,
  onOpenProductPage,
}: ProductDetailProps) {
  const [showAllReviews, setShowAllReviews] = useState(false)
  const individualReviews = getProductReviews(item.item_id)
  const visibleReviews = showAllReviews ? individualReviews : individualReviews.slice(0, 2)
  const reviewScore = individualReviews.length > 0
    ? individualReviews.reduce((total, entry) => total + entry.rating, 0) / individualReviews.length
    : 0
  const roundedReviewScore = Math.round(reviewScore)

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
            <div><dt>素材</dt><dd>{item.material}</dd></div>
            <div className="product-introduction"><dt>商品紹介</dt><dd>{item.web_description}</dd></div>
          </dl>
          <a
            className="secondary-button official-page-action official-page-inline-action"
            data-t2-action={`product_page_open:${item.item_id}`}
            href={item.product_url}
            onClick={() => onOpenProductPage(item)}
          >
            商品ページを開く
          </a>
          {individualReviews.length > 0 && (
            <section className="customer-reviews" aria-labelledby="customer-reviews-title">
              <div className="review-heading">
                <div>
                  <p className="review-kicker">CUSTOMER REVIEWS</p>
                  <h2 id="customer-reviews-title">商品レビュー</h2>
                </div>
                <div className="review-score" aria-label={`参考評価 5点中${reviewScore.toFixed(1)}点`}>
                  <span className="review-score-number">{reviewScore.toFixed(1)}</span>
                  <span className="review-stars" aria-hidden="true">
                    {'★'.repeat(roundedReviewScore)}
                    <span className="empty-stars">{'★'.repeat(5 - roundedReviewScore)}</span>
                  </span>
                  <span className="review-score-scale">{individualReviews.length}件のレビュー</span>
                </div>
              </div>
              <div className="review-list">
                {visibleReviews.map((entry, index) => (
                  <article className="customer-review" key={`${item.item_id}-${index}`}>
                    <div className="customer-review-meta">
                      <span className="review-avatar" aria-hidden="true">
                        {String(index + 1).padStart(2, '0')}
                      </span>
                      <div>
                        <h3>{entry.title}</h3>
                        <p className="individual-stars" aria-label={`5点中${entry.rating}点`}>
                          {'★'.repeat(entry.rating)}
                          <span className="empty-stars">{'★'.repeat(5 - entry.rating)}</span>
                        </p>
                      </div>
                    </div>
                    <p className="customer-review-body">{entry.body}</p>
                  </article>
                ))}
              </div>
              {individualReviews.length > 2 && (
                <button
                  className="show-more-reviews"
                  type="button"
                  aria-expanded={showAllReviews}
                  onClick={() => setShowAllReviews((current) => !current)}
                >
                  {showAllReviews
                    ? 'レビューを閉じる'
                    : `残り${individualReviews.length - 2}件のレビューをもっと見る`}
                </button>
              )}
              <p className="review-disclaimer">
                Web上で公開されているレビューや紹介記事を参考に、個人情報を含まない表現へ再構成しています。
              </p>
            </section>
          )}
          <div className="detail-actions">
            <button
              className="primary-button candidate-action"
              data-t2-action={`add_candidate:${item.item_id}`}
              onClick={() => onAddCandidate(item)}
              disabled={isCandidate}
            >
              {isCandidate ? '候補に追加済み' : '候補に追加'}
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
