interface HomeProps {
  isPracticeMode?: boolean
  onOpenCategories: () => void
  onOpenCandidates: () => void
  candidateCount: number
}

export default function Home({
  isPracticeMode = false,
  onOpenCategories,
  onOpenCandidates,
  candidateCount,
}: HomeProps) {
  return (
    <main className="home screen" aria-labelledby="home-title">
      <p className="eyebrow">{isPracticeMode ? 'T2 PRACTICE WEB' : 'T2 EXPERIMENT WEB'}</p>
      <h1 id="home-title">{isPracticeMode ? '操作練習用Web' : 'キャンプギア比較Web'}</h1>
      <p className="lead">
        {isPracticeMode
          ? '本番とは異なる架空の商品を使って、スクロールや候補操作を練習します。'
          : '動画で気になったキャンプギアを、カテゴリから探して比較できます。'}
      </p>
      <div className="home-actions">
        <button className="primary-button" onClick={onOpenCategories}>
          カテゴリから探す
        </button>
        <button className="secondary-button" onClick={onOpenCandidates}>
          候補リストを見る
          <span className="count-badge">{candidateCount}</span>
        </button>
      </div>
    </main>
  )
}
