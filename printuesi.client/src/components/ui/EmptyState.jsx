export default function EmptyState({ title, description }) {
    return (
        <div className="empty-state">
            <div className="empty-icon">□</div>
            <h5>{title}</h5>
            {description && <p>{description}</p>}
        </div>
    );
}