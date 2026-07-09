import type { MouseEvent } from 'react'
import type { Item } from '../types'
import ProductImage from './ProductImage'

interface ProductCardProps {
  item: Item
  orderNumber: number
  onOpen: (item: Item) => void
}

export default function ProductCard({ item, orderNumber, onOpen }: ProductCardProps) {
  const open = () => onOpen(item)

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
