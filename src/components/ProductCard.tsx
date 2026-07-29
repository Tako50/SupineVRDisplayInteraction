import type { MouseEvent } from 'react'
import type { Item } from '../types'
import { getProductReviews } from '../data/reviewDetails'
import ProductImage from './ProductImage'

interface ProductCardProps {
  item: Item
  orderNumber: number
  onOpen: (item: Item) => void
}

export default function ProductCard({ item, orderNumber, onOpen }: ProductCardProps) {
  const open = () => onOpen(item)
  const reviews = getProductReviews(item.item_id)
  const reviewScore = reviews.length > 0
    ? reviews.reduce((total, entry) => total + entry.rating, 0) / reviews.length
    : 0
  const roundedReviewScore = Math.round(reviewScore)

  return (
    <article
      className="product-card"
      data-t2-action={`product_open:${item.item_id}`}
      onClick={open}
    >
      <ProductImage item={item} className="product-card-image" />
      <div className="product-card-body">
        <p className="product-card-order">{orderNumber}番</p>
        <h2>{item.product_name}</h2>
        {reviews.length > 0 && (
          <div className="card-review-score" aria-label={`参考評価 5点中${reviewScore.toFixed(1)}点`}>
            <span className="card-stars" aria-hidden="true">
              {'★'.repeat(roundedReviewScore)}
              <span className="empty-stars">{'★'.repeat(5 - roundedReviewScore)}</span>
            </span>
            <strong>{reviewScore.toFixed(1)}</strong>
            <span>参考評価</span>
          </div>
        )}
        <button
          className="primary-button compact"
          onClick={(event: MouseEvent<HTMLButtonElement>) => {
            event.stopPropagation()
            open()
          }}
        >
          詳細を見る
        </button>
      </div>
    </article>
  )
}
