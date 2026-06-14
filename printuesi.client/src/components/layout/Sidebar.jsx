import { NavLink } from "react-router-dom";
import { navigationItems } from "../../data/navigation";
import { useAuth } from "../../contexts/auth/useAuth";
import { useLanguage } from "../../contexts/language/useLanguage";

export default function Sidebar({ open, onClose }) {
    const { isAdmin } = useAuth();
    const { t } = useLanguage();

    const visibleItems = navigationItems.filter((item) => {
        if (item.adminOnly && !isAdmin) return false;
        return true;
    });

    return (
        <aside className={`app-sidebar ${open ? "open" : ""}`}>
            <div className="sidebar-brand">
                <div className="brand-mark">P</div>
                <div>
                    <div className="brand-title">{t("appName")}</div>
                    <div className="brand-subtitle">Epson CW-6000AE</div>
                </div>
            </div>

            <nav className="sidebar-nav">
                {visibleItems.map((item) => (
                    <NavLink
                        key={item.path}
                        to={item.path}
                        onClick={onClose}
                        className={({ isActive }) =>
                            `sidebar-link ${isActive ? "active" : ""}`
                        }
                    >
                        <span className="sidebar-icon">{item.icon}</span>
                        <span>{t(item.labelKey)}</span>
                    </NavLink>
                ))}
            </nav>
        </aside>
    );
}