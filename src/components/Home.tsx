interface HomeProps {
  onOpenCategories: () => void
  onOpenCandidates: () => void
  candidateCount: number
}

export default function Home({ onOpenCategories, onOpenCandidates, candidateCount }: HomeProps) {
  return (
    <main className="home screen" aria-labelledby="home-title">
      <p className="eyebrow">T2 EXPERIMENT WEB</p>
      <h1 id="home-title">キャンプギア比較Web</h1>
      <p className="lead">動画で気になったキャンプギアを、カテゴリから探して比較できます。</p>
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
