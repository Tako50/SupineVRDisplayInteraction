import { useEffect, useState } from 'react'
import type { Item } from '../types'

interface ProductImageProps {
  item: Item
  className?: string
}

function resolveImagePath(path: string): string {
  const relativePath = path.replace(/^\//, '')
  return `${import.meta.env.BASE_URL}${relativePath}`
}

export default function ProductImage({ item, className = '' }: ProductImageProps) {
  const [failed, setFailed] = useState(false)

  useEffect(() => setFailed(false), [item.image])

  if (failed || !item.image) {
    return (
      <div className={`image-fallback ${className}`} role="img" aria-label={`${item.product_name}の代替表示`}>
        <strong>{item.product_name}</strong>
      </div>
    )
  }

  return (
    <img
      className={className}
      src={resolveImagePath(item.image)}
      alt={item.product_name}
      onError={() => setFailed(true)}
    />
  )
}
