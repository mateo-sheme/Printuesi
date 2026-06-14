import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./contexts/auth/useAuth";

import AppLayout from "./components/layout/AppLayout";
import Dashboard from "./pages/Dashboard";
import Login from "./pages/Login";
import AutomaticPrinting from "./pages/AutomaticPrinting";
import ManualPrinting from "./pages/ManualPrinting";
import PlaceholderPage from "./pages/PlaceholderPage";

function ProtectedRoute({ children }) {
    const { isAuthenticated } = useAuth();

    if (!isAuthenticated) {
        return <Navigate to="/login" replace />;
    }

    return children;
}

function AdminRoute({ children }) {
    const { isAuthenticated, isAdmin } = useAuth();

    if (!isAuthenticated) {
        return <Navigate to="/login" replace />;
    }

    if (!isAdmin) {
        return <Navigate to="/dashboard" replace />;
    }

    return children;
}

export default function App() {
    return (
        <BrowserRouter>
            <Routes>
                <Route path="/login" element={<Login />} />

                <Route
                    path="/"
                    element={
                        <ProtectedRoute>
                            <AppLayout />
                        </ProtectedRoute>
                    }
                >
                    <Route index element={<Navigate to="/dashboard" replace />} />
                    <Route path="dashboard" element={<Dashboard />} />
                    <Route path="automatic-printing" element={<AutomaticPrinting />} />
                    <Route path="manual-printing" element={<ManualPrinting />} />

                    <Route path="clients" element={<PlaceholderPage pageKey="clients" />} />
                    <Route path="templates" element={<PlaceholderPage pageKey="templates" />} />
                    <Route path="print-history" element={<PlaceholderPage pageKey="printHistory" />} />

                    <Route
                        path="supplies"
                        element={
                            <AdminRoute>
                                <PlaceholderPage pageKey="supplies" />
                            </AdminRoute>
                        }
                    />

                    <Route
                        path="users"
                        element={
                            <AdminRoute>
                                <PlaceholderPage pageKey="users" />
                            </AdminRoute>
                        }
                    />

                    <Route path="settings" element={<PlaceholderPage pageKey="settings" />} />
                </Route>

                <Route path="*" element={<Navigate to="/dashboard" replace />} />
            </Routes>
        </BrowserRouter>
    );
}