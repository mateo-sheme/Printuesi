import { useEffect, useMemo, useState } from "react";
import { usersApi } from "../api/usersApi";

const ROLE_OPTIONS = ["User", "Admin"];

function getValue(object, camelKey, pascalKey, fallback = "") {
    return object?.[camelKey] ?? object?.[pascalKey] ?? fallback;
}

function formatDate(value) {
    if (!value) return "-";

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) return "-";

    return date.toLocaleString();
}

function getUserID(user) {
    return getValue(user, "userID", "UserID");
}

function getUsername(user) {
    return getValue(user, "username", "Username", "");
}

function getUserDisplayName(user) {
    return (
        getValue(user, "name", "Name", "") ||
        getValue(user, "fullName", "FullName", "") ||
        getUsername(user)
    );
}

function getRole(user) {
    return getValue(user, "role", "Role", "User");
}

function getIsActive(user) {
    return getValue(user, "isActive", "IsActive", true);
}

export default function Users() {
    const [users, setUsers] = useState([]);
    const [selectedUser, setSelectedUser] = useState(null);

    const [search, setSearch] = useState("");
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);

    const [error, setError] = useState("");
    const [message, setMessage] = useState("");

    const [form, setForm] = useState({
        username: "",
        password: "",
        role: "User",
        isActive: true
    });

    const [passwordForm, setPasswordForm] = useState({
        password: "",
        confirmPassword: ""
    });

    async function loadUsers() {
        setLoading(true);
        setError("");

        try {
            const result = await usersApi.getAll();
            setUsers(Array.isArray(result) ? result : []);
        } catch (err) {
            setError(err.message || "Failed to load users.");
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        loadUsers();
    }, []);

    const filteredUsers = useMemo(() => {
        const term = search.trim().toLowerCase();

        if (!term) return users;

        return users.filter((user) => {
            const username = getUsername(user).toLowerCase();
            const role = getRole(user).toLowerCase();

            return username.includes(term) || role.includes(term);
        });
    }, [users, search]);

    function resetForm() {
        setSelectedUser(null);
        setForm({
            username: "",
            password: "",
            role: "User",
            isActive: true
        });
        setPasswordForm({
            password: "",
            confirmPassword: ""
        });
        setError("");
        setMessage("");
    }

    function selectUser(user) {
        setSelectedUser(user);

        setForm({
            username: getUsername(user),
            password: "",
            role: getRole(user),
            isActive: getIsActive(user)
        });

        setPasswordForm({
            password: "",
            confirmPassword: ""
        });

        setError("");
        setMessage("");
    }

    async function saveUser(event) {
        event.preventDefault();

        setError("");
        setMessage("");

        const username = form.username.trim();

        if (!username) {
            setError("Username is required.");
            return;
        }

        if (!selectedUser && !form.password.trim()) {
            setError("Password is required for new users.");
            return;
        }

        setSaving(true);

        try {
            if (selectedUser) {
                const userID = getUserID(selectedUser);

                await usersApi.updateUser(userID, {
                    username,
                    role: form.role,
                    isActive: form.isActive
                });

                setMessage("User updated.");
            } else {
                await usersApi.createUser({
                    username,
                    password: form.password,
                    role: form.role
                });

                setMessage("User created.");
            }

            await loadUsers();
            resetForm();
        } catch (err) {
            setError(err.message || "Failed to save user.");
        } finally {
            setSaving(false);
        }
    }

    async function changePassword(event) {
        event.preventDefault();

        if (!selectedUser) {
            setError("Select a user first.");
            return;
        }

        if (!passwordForm.password.trim()) {
            setError("New password is required.");
            return;
        }

        if (passwordForm.password !== passwordForm.confirmPassword) {
            setError("Passwords do not match.");
            return;
        }

        const confirmed = window.confirm(
            `Change password for ${getUsername(selectedUser)}?`
        );

        if (!confirmed) return;

        setSaving(true);
        setError("");
        setMessage("");

        try {
            await usersApi.changePassword(
                getUserID(selectedUser),
                passwordForm.password
            );

            setPasswordForm({
                password: "",
                confirmPassword: ""
            });

            setMessage("Password changed.");
        } catch (err) {
            setError(err.message || "Failed to change password.");
        } finally {
            setSaving(false);
        }
    }

    async function deleteSelectedUser() {
        if (!selectedUser) {
            setError("Select a user first.");
            return;
        }

        const username = getUsername(selectedUser);

        const confirmed = window.confirm(
            `Delete user "${username}"? This cannot be undone.`
        );

        if (!confirmed) return;

        setSaving(true);
        setError("");
        setMessage("");

        try {
            await usersApi.deleteUser(getUserID(selectedUser));

            setMessage("User deleted.");
            await loadUsers();
            resetForm();
        } catch (err) {
            setError(err.message || "Failed to delete user.");
        } finally {
            setSaving(false);
        }
    }

    return (
        <div className="page-stack">
            <section className="hero-panel compact-hero">
                <div>
                    <span className="eyebrow">Përdoruesit</span>
                    <h1>User Management</h1>
                    <p>
                        Create users, change passwords, and assign User/Admin
                        permissions.
                    </p>
                </div>

                <button className="btn btn-primary-gradient" onClick={resetForm}>
                    New user
                </button>
            </section>

            {error && <div className="alert alert-danger">{error}</div>}
            {message && <div className="alert alert-success">{message}</div>}

            <section className="users-layout">
                <div className="panel-card compact-panel users-list-panel">
                    <div className="panel-header">
                        <div>
                            <h5>Users</h5>
                            <p>Only admins can manage users.</p>
                        </div>

                        <input
                            className="form-control users-search-box"
                            value={search}
                            onChange={(event) => setSearch(event.target.value)}
                            placeholder="Search users..."
                        />
                    </div>

                    {loading ? (
                        <div className="small text-muted">Loading users...</div>
                    ) : filteredUsers.length === 0 ? (
                        <div className="empty-state compact-empty">
                            <div className="empty-icon">□</div>
                            <h5>No users found</h5>
                            <p>No users match your search.</p>
                        </div>
                    ) : (
                        <div className="users-card-list">
                            {filteredUsers.map((user) => {
                                const userID = getUserID(user);
                                const username = getUsername(user);
                                const role = getRole(user);
                                const isActive = getIsActive(user);
                                const createdAt = getValue(
                                    user,
                                    "createdAt",
                                    "CreatedAt"
                                );

                                const isSelected =
                                    selectedUser &&
                                    getUserID(selectedUser) === userID;

                                return (
                                    <button
                                        type="button"
                                        key={userID}
                                        className={`user-list-card ${isSelected ? "selected" : ""
                                            }`}
                                        onClick={() => selectUser(user)}
                                    >
                                        <div>
                                            <strong>{getUserDisplayName(user)}</strong>
                                            <small>{userID}</small>
                                        </div>

                                        <div className="user-card-meta">
                                            <span
                                                className={`job-badge ${role === "Admin"
                                                        ? "progress"
                                                        : "pending"
                                                    }`}
                                            >
                                                {role}
                                            </span>

                                            <span
                                                className={`job-badge ${isActive
                                                        ? "done"
                                                        : "cancelled"
                                                    }`}
                                            >
                                                {isActive ? "Active" : "Inactive"}
                                            </span>
                                        </div>

                                        <small>{formatDate(createdAt)}</small>
                                    </button>
                                );
                            })}
                        </div>
                    )}
                </div>

                <div className="panel-card compact-panel users-editor-panel">
                    <div className="panel-header">
                        <div>
                            <span className="eyebrow">
                                {selectedUser ? "Edit user" : "Create user"}
                            </span>
                            <h5>
                                {selectedUser
                                    ? getUsername(selectedUser)
                                    : "New user"}
                            </h5>
                            <p>
                                User role gives access to all pages except
                                Përdoruesit. Admin can access everything.
                            </p>
                        </div>
                    </div>

                    <form onSubmit={saveUser} className="user-form">
                        <div>
                            <label className="form-label">Username</label>
                            <input
                                className="form-control"
                                value={form.username}
                                onChange={(event) =>
                                    setForm((prev) => ({
                                        ...prev,
                                        username: event.target.value
                                    }))
                                }
                                disabled={saving}
                            />
                        </div>

                        {!selectedUser && (
                            <div>
                                <label className="form-label">Password</label>
                                <input
                                    className="form-control"
                                    type="password"
                                    value={form.password}
                                    onChange={(event) =>
                                        setForm((prev) => ({
                                            ...prev,
                                            password: event.target.value
                                        }))
                                    }
                                    disabled={saving}
                                />
                            </div>
                        )}

                        <div>
                            <label className="form-label">Role</label>
                            <select
                                className="form-select"
                                value={form.role}
                                onChange={(event) =>
                                    setForm((prev) => ({
                                        ...prev,
                                        role: event.target.value
                                    }))
                                }
                                disabled={saving}
                            >
                                {ROLE_OPTIONS.map((role) => (
                                    <option key={role} value={role}>
                                        {role}
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div className="form-check">
                            <input
                                className="form-check-input"
                                type="checkbox"
                                id="isActive"
                                checked={form.isActive}
                                onChange={(event) =>
                                    setForm((prev) => ({
                                        ...prev,
                                        isActive: event.target.checked
                                    }))
                                }
                                disabled={saving}
                            />

                            <label
                                className="form-check-label"
                                htmlFor="isActive"
                            >
                                Active user
                            </label>
                        </div>

                        <div className="user-form-actions">
                            <button
                                className="btn btn-primary-gradient"
                                type="submit"
                                disabled={saving}
                            >
                                {saving
                                    ? "Saving..."
                                    : selectedUser
                                        ? "Save changes"
                                        : "Create user"}
                            </button>

                            {selectedUser && (
                                <button
                                    type="button"
                                    className="btn btn-outline-danger"
                                    disabled={saving}
                                    onClick={deleteSelectedUser}
                                >
                                    Delete user
                                </button>
                            )}
                        </div>
                    </form>

                    {selectedUser && (
                        <form
                            onSubmit={changePassword}
                            className="user-password-box mt-4"
                        >
                            <h6>Change password</h6>

                            <div>
                                <label className="form-label">
                                    New password
                                </label>
                                <input
                                    className="form-control"
                                    type="password"
                                    value={passwordForm.password}
                                    onChange={(event) =>
                                        setPasswordForm((prev) => ({
                                            ...prev,
                                            password: event.target.value
                                        }))
                                    }
                                    disabled={saving}
                                />
                            </div>

                            <div>
                                <label className="form-label">
                                    Confirm password
                                </label>
                                <input
                                    className="form-control"
                                    type="password"
                                    value={passwordForm.confirmPassword}
                                    onChange={(event) =>
                                        setPasswordForm((prev) => ({
                                            ...prev,
                                            confirmPassword:
                                                event.target.value
                                        }))
                                    }
                                    disabled={saving}
                                />
                            </div>

                            <button
                                className="btn btn-soft"
                                type="submit"
                                disabled={saving}
                            >
                                Change password
                            </button>
                        </form>
                    )}
                </div>
            </section>
        </div>
    );
}