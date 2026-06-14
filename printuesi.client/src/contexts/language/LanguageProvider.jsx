import { useCallback, useMemo, useState } from "react";
import { translations } from "../../i18n/translations";
import { LanguageContext, LANGUAGE_STORAGE_KEY } from "./LanguageContext";

export default function LanguageProvider({ children }) {
    const [language, setLanguage] = useState(
        localStorage.getItem(LANGUAGE_STORAGE_KEY) || "sq"
    );

    const changeLanguage = useCallback((nextLanguage) => {
        const safeLanguage = nextLanguage === "en" ? "en" : "sq";
        localStorage.setItem(LANGUAGE_STORAGE_KEY, safeLanguage);
        setLanguage(safeLanguage);
    }, []);

    const toggleLanguage = useCallback(() => {
        setLanguage((currentLanguage) => {
            const nextLanguage = currentLanguage === "sq" ? "en" : "sq";
            localStorage.setItem(LANGUAGE_STORAGE_KEY, nextLanguage);
            return nextLanguage;
        });
    }, []);

    const t = useCallback(
        (key) => {
            return translations[language]?.[key] || translations.en[key] || key;
        },
        [language]
    );

    const value = useMemo(
        () => ({
            language,
            changeLanguage,
            toggleLanguage,
            t
        }),
        [language, changeLanguage, toggleLanguage, t]
    );

    return (
        <LanguageContext.Provider value={value}>
            {children}
        </LanguageContext.Provider>
    );
}