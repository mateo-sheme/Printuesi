import { useEffect, useMemo, useState } from "react";
import { ThemeContext, THEME_STORAGE_KEY } from "./ThemeContext";

export default function ThemeProvider({ children }) {
    const [theme, setTheme] = useState(
        localStorage.getItem(THEME_STORAGE_KEY) || "light"
    );

    useEffect(() => {
        document.documentElement.setAttribute("data-theme", theme);
        document.body.className = theme === "dark" ? "app-dark" : "app-light";
        localStorage.setItem(THEME_STORAGE_KEY, theme);
    }, [theme]);

    const toggleTheme = () => {
        setTheme((current) => (current === "dark" ? "light" : "dark"));
    };

    const value = useMemo(
        () => ({
            theme,
            isDark: theme === "dark",
            toggleTheme
        }),
        [theme]
    );

    return (
        <ThemeContext.Provider value={value}>
            {children}
        </ThemeContext.Provider>
    );
}