import type { Item } from '../types'

interface CandidateListProps {
  candidates: Item[]
  confirmedIds: string[] | null
  onRemove: (item: Item) => void
  onConfirm: () => void
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
        <p>追加した商品から、候補を2〜3個に絞って確定してください。</p>
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
                className="danger-button"
                data-t2-action={`remove_candidate:${item.item_id}`}
                onClick={() => onRemove(item)}
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

      <button
        className="confirm-button"
        data-t2-candidate={candidates.map((item) => item.item_id).join(',')}
        onClick={onConfirm}
        disabled={candidates.length < 2 || candidates.length > 3}
      >
        候補を確定（2〜3個）
      </button>

      {confirmedIds && (
        <section className="confirmation" aria-live="polite">
          <strong>{confirmedIds.length}個の候補を確定しました</strong>
        </section>
      )}
    </main>
  )
}
