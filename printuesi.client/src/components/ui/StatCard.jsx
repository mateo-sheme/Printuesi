export default function StatCard({ title, value, helper, tone = "primary" }) {
    return (
        <div className={`stat-card stat-${tone}`}>
            <div className="stat-title">{title}</div>
            <div className="stat-value">{value}</div>
            {helper && <div className="stat-helper">{helper}</div>}
        </div>
    );
}