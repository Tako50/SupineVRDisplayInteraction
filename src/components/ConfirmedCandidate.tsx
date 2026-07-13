import type { Item } from '../types'
import ProductImage from './ProductImage'

interface ConfirmedCandidateProps {
  item: Item
  onResetAndReturnHome: () => void
}

export default function ConfirmedCandidate({
  item,
  onResetAndReturnHome,
}: ConfirmedCandidateProps) {
  return (
    <main className="screen confirmed-screen" aria-labelledby="confirmed-title">
      <section className="confirmed-card" aria-live="polite">
        <div className="confirmed-heading">
          <p className="eyebrow">TASK COMPLETE</p>
          <h1 id="confirmed-title">最終候補を確定しました</h1>
          <p>
            終了後アンケートでは、下の商品を選んだ理由を簡単に答えてください。
            アンケートが終わるまで、この画面を表示したままにしてください。
          </p>
        </div>

        <div className="confirmed-product">
          <ProductImage item={item} className="confirmed-product-image" />
          <div className="confirmed-product-info">
            <p className="eyebrow">SELECTED ITEM</p>
            <h2>{item.product_name}</h2>
            <p className="brand confirmed-brand">{item.brand}</p>
            <dl className="confirmed-specs">
              <div><dt>カテゴリ</dt><dd>{item.category}</dd></div>
              <div><dt>価格</dt><dd>{item.price}</dd></div>
              <div><dt>サイズ</dt><dd>{item.size}</dd></div>
              <div><dt>重量</dt><dd>{item.weight}</dd></div>
            </dl>
          </div>
        </div>

        <div className="confirmed-actions">
          <p>アンケート回答後、次の実験を始める前に押してください。</p>
          <button className="reset-home-button" onClick={onResetAndReturnHome}>
            候補リストを削除してホームに戻る
          </button>
        </div>
      </section>
    </main>
  )
}
