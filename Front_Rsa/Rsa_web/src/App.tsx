import * as React from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Login from './components/Login';
import Register from './components/Register';
import Dashboard from './components/Dashboard';
import HomePage from './components/Home_Page';
import SignatureManagement from './components/Signature_Management';
import Sign_file from './components/Sign_file';
import Verify_file from './components/Verify_file';

const App: React.FC = () => {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/" element={<Login />} />
        <Route path="/homepage" element={<HomePage />} />
        <Route path="/signatures" element={<SignatureManagement />} />
        <Route path="/sign_file" element={<Sign_file />} />
        <Route path="/verify_file" element={<Verify_file />} />
      </Routes>
    </BrowserRouter>
  );
};

export default App;