import type { Item } from '../types'

interface CandidateListProps {
  candidates: Item[]
  confirmedIds: string[] | null
  onRemove: (item: Item) => void
  onConfirm: (item: Item) => void
  onBack: () => void
}

export default function CandidateList({
  candidates,
  confirmedIds,
  onRemove,
  onConfirm,
  onBack,
}: CandidateListProps) {
  return (
    <main className="screen candidates-screen" aria-labelledby="candidates-title">
      <button className="back-button" onClick={onBack}>← ホームに戻る</button>
      <div className="page-heading">
        <p className="eyebrow">CANDIDATES</p>
        <h1 id="candidates-title">候補リスト</h1>
        <p>追加した商品から、最終候補を1つ選んで確定してください。</p>
      </div>

      {candidates.length > 0 ? (
        <div className="candidate-list">
          {candidates.map((item) => (
            <article className="candidate-row" key={item.item_id}>
              <div>
                <h2>{item.product_name}</h2>
                <p>{item.brand} · {item.price}</p>
              </div>
              <button
                className="final-button"
                data-t2-candidate={item.item_id}
                onClick={() => onConfirm(item)}
                disabled={confirmedIds !== null}
              >
                この商品に決定
              </button>
              <button
                className="danger-button"
                data-t2-action={`remove_candidate:${item.item_id}`}
                onClick={() => onRemove(item)}
                disabled={confirmedIds !== null}
              >
                削除
              </button>
            </article>
          ))}
        </div>
      ) : (
        <div className="empty-state">
          <h2>候補はまだありません</h2>
          <p>カテゴリから商品詳細を開き、候補に追加してください。</p>
        </div>
      )}

      {confirmedIds && (
        <section className="confirmation" aria-live="polite">
          <strong>最終候補を確定しました</strong>
        </section>
      )}
    </main>
  )
}
