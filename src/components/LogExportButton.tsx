import { exportLogs } from '../utils/logger'

export default function LogExportButton() {
  return (
    <button className="log-export-button" onClick={exportLogs}>
      ログを書き出す
    </button>
  )
}
