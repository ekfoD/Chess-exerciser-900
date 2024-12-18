import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import '../styles/Login.css';
import { useAuth } from './AuthContext';

const Login = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const navigate = useNavigate();
    const { setToken } = useAuth();

    const handleLogin = async (e) => {
        e.preventDefault();

        const response = await fetch(
            "http://localhost:5030/api/chess/login",
            {
                method: "POST",
                body: JSON.stringify({
                    Email: email,
                    Password: password
                }),
                headers: {
                    "Content-type": "application/json; charset=UTF-8",
                },
            }
        );

        const data = await response.json();

        if (data.token !== null) {
            setToken(data.token);
            localStorage.setItem("isLoggedIn", "true");
            localStorage.setItem("email", email);
            alert("Login successful!");
            navigate("/play");
        } else {
            alert("Invalid credentials. Please try again.");
        }
    };

    return (
        <div className="login-container">
            <div className="login-card">
                <h2 className="login-title">WELCOME BACK, <br /> CHESS MASTER</h2>
                <form onSubmit={handleLogin}>
                    <div className="mb-3">
                        <input
                            type="text"
                            className="form-control login-input"
                            placeholder="Email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                        />
                    </div>
                    <div className="mb-3">
                        <input
                            type={showPassword ? "text" : "password"}
                            className="form-control login-input"
                            placeholder="Password"
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                        />
                        <div className="d-flex align-items-center">
                            <input
                                type="checkbox"
                                className="form-check-input"
                                id="showPassword"
                                checked={showPassword}
                                onChange={() => setShowPassword(!showPassword)}
                                style={{ marginTop: 20 }}
                            />
                            <label
                                htmlFor="showPassword"
                                className="text-muted small"
                                style={{
                                    fontSize: '0.7rem',
                                    cursor: 'pointer',
                                    lineHeight: 1,
                                    marginBottom: 0
                                }}
                            >
                                Show password
                            </label>
                        </div>
                    </div>
                    <button
                        type="submit"
                        className="btn w-100 mb-3 login-btn"
                    >
                        Login
                    </button>
                    <div className="text-center">
                        <Link
                            to="/register"
                            className="login-link"
                        >
                            Create an Account
                        </Link>
                    </div>
                </form>
            </div>
        </div>
    );
};

export default Login;