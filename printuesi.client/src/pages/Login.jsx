import { useState } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "../contexts/auth/useAuth";
import { useLanguage } from "../contexts/language/useLanguage";
import { useTheme } from "../contexts/theme/useTheme";

export default function Login() {
    const { login, isAuthenticated } = useAuth();
    const { t, toggleLanguage, language } = useLanguage();
    const { isDark, toggleTheme } = useTheme();

    const [form, setForm] = useState({
        name: "",
        password: ""
    });

    const [error, setError] = useState("");
    const [loading, setLoading] = useState(false);

    if (isAuthenticated) {
        return <Navigate to="/dashboard" replace />;
    }

    const handleChange = (event) => {
        const { name, value } = event.target;
        setForm((current) => ({
            ...current,
            [name]: value
        }));
    };

    const handleSubmit = async (event) => {
        event.preventDefault();
        setError("");
        setLoading(true);

        try {
            await login(form);
        } catch (err) {
            setError(err.message || "Login failed.");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="login-page">
            <div className="login-card">
                <div className="d-flex justify-content-between align-items-center mb-4">
                    <div className="brand-mark large">P</div>

                    <div className="d-flex gap-2">
                        <button className="btn btn-soft" onClick={toggleLanguage}>
                            {language === "sq" ? "EN" : "SQ"}
                        </button>
                        <button className="btn btn-soft" onClick={toggleTheme}>
                            {isDark ? "☀" : "☾"}
                        </button>
                    </div>
                </div>

                <h1>{t("loginTitle")}</h1>
                <p className="text-muted">{t("loginSubtitle")}</p>

                {error && <div className="alert alert-danger">{error}</div>}

                <form onSubmit={handleSubmit} className="mt-4">
                    <div className="mb-3">
                        <label className="form-label">{t("username")}</label>
                        <input
                            className="form-control form-control-lg"
                            name="name"
                            value={form.name}
                            onChange={handleChange}
                            autoComplete="username"
                            required
                        />
                    </div>

                    <div className="mb-4">
                        <label className="form-label">{t("password")}</label>
                        <input
                            className="form-control form-control-lg"
                            type="password"
                            name="password"
                            value={form.password}
                            onChange={handleChange}
                            autoComplete="current-password"
                            required
                        />
                    </div>

                    <button className="btn btn-primary-gradient btn-lg w-100" disabled={loading}>
                        {loading ? t("loading") : t("login")}
                    </button>
                </form>
            </div>
        </div>
    );
}