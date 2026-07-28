import type { Item } from '../types'

interface CandidateListProps {
  candidates: Item[]
  pendingConfirmationId: string | null
  confirmationError: string
  confirmationLocked: boolean
  confirmationUnlockRemainingSeconds: number
  onRemove: (item: Item) => void
  onConfirm: (item: Item) => void
  onBack: () => void
}

export default function CandidateList({
  candidates,
  pendingConfirmationId,
  confirmationError,
  confirmationLocked,
  confirmationUnlockRemainingSeconds,
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
        {confirmationLocked && (
          <p className="confirmation-lock-notice" aria-live="polite">
            最終候補は本番開始から11分30秒後に確定できます（あと{formatRemainingTime(confirmationUnlockRemainingSeconds)}）
          </p>
        )}
      </div>

      {candidates.length > 0 ? (
        <div className="candidate-list">
          {candidates.map((item) => (
            <article className="candidate-row" key={item.item_id}>
              <div>
                <h2>{item.product_name}</h2>
                <p>{item.brand} · {item.category}</p>
              </div>
              <button
                className="final-button"
                data-t2-candidate={item.item_id}
                onClick={() => onConfirm(item)}
                disabled={confirmationLocked || pendingConfirmationId !== null}
              >
                {confirmationLocked
                  ? `決定まで ${formatRemainingTime(confirmationUnlockRemainingSeconds)}`
                  : pendingConfirmationId === item.item_id
                    ? '確定処理中…'
                    : 'この商品に決定'}
              </button>
              <button
                className="danger-button"
                data-t2-action={`remove_candidate:${item.item_id}`}
                onClick={() => onRemove(item)}
                disabled={pendingConfirmationId !== null}
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

      {confirmationError && (
        <section className="confirmation-error" role="alert">
          <strong>候補を確定できませんでした</strong>
          <p>{confirmationError}</p>
        </section>
      )}
    </main>
  )
}

function formatRemainingTime(totalSeconds: number) {
  const seconds = Math.max(0, Math.ceil(totalSeconds))
  const minutes = Math.floor(seconds / 60)
  return `${minutes}:${String(seconds % 60).padStart(2, '0')}`
}
