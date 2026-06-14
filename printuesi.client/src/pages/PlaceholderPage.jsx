import { useAuth } from "../contexts/auth/useAuth";
import { useLanguage } from "../contexts/language/useLanguage";
import { useTheme } from "../contexts/theme/useTheme";

export default function PlaceholderPage({ pageKey }) {
    const { t } = useLanguage();

    return (
        <div className="page-stack">
            <section className="hero-panel">
                <div>
                    <span className="eyebrow">Printuesi</span>
                    <h1>{t(pageKey)}</h1>
                    <p>{t("notBuiltYet")}</p>
                </div>
            </section>

            <div className="panel-card">
                <p className="mb-0 text-muted">
                    This section is intentionally reserved. The dashboard, automatic
                    printing, and manual printing are the first priority.
                </p>
            </div>
        </div>
    );
}