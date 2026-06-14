import { useAuth } from "../../contexts/auth/useAuth";
import { useLanguage } from "../../contexts/language/useLanguage";
import { useTheme } from "../../contexts/theme/useTheme";

export default function Topbar({ onMenuClick }) {
    const { user, logout, isAdmin } = useAuth();
    const { language, toggleLanguage, t } = useLanguage();
    const { isDark, toggleTheme } = useTheme();

    return (
        <header className="app-topbar">
            <button className="btn btn-soft d-lg-none" onClick={onMenuClick}>
                ☰
            </button>

            <div className="topbar-title">
                <span className="text-muted small">Printuesi</span>
                <strong>Epson Label Automation</strong>
            </div>

            <div className="topbar-actions">
                <button className="btn btn-soft" onClick={toggleLanguage}>
                    {language === "sq" ? "EN" : "SQ"}
                </button>

                <button className="btn btn-soft" onClick={toggleTheme}>
                    {isDark ? "☀" : "☾"}
                </button>

                <div className="user-pill d-none d-sm-flex">
                    <span className="user-avatar">{user?.name?.[0]?.toUpperCase() || "U"}</span>
                    <span>
                        <strong>{user?.name}</strong>
                        <small>{isAdmin ? t("roleAdmin") : t("roleUser")}</small>
                    </span>
                </div>

                <button className="btn btn-primary-gradient" onClick={logout}>
                    {t("logout")}
                </button>
            </div>
        </header>
    );
}