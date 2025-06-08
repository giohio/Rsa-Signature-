import * as React from 'react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { Box, Paper, Typography, TextField, Button, Link as MuiLink, Alert, InputAdornment, IconButton } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import Person from '@mui/icons-material/Person';
import Lock from '@mui/icons-material/Lock';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';

const API_URL = '/api';

const Login: React.FC = () => {
  const [username, setUsername] = useState<string>('');
  const [password, setPassword] = useState<string>('');
  const [showPassword, setShowPassword] = useState<boolean>(false);
  const [error, setError] = useState<string>('');
  const navigate = useNavigate();

  const handleTogglePassword = () => setShowPassword(prev => !prev);

  const handleLogin = async (e: React.MouseEvent<HTMLButtonElement>) => {
    e.preventDefault();
    try {
      const response = await axios.post(`${API_URL}/user/login`, { username, password });
      localStorage.setItem('userId', response.data.userId);
      localStorage.setItem('fullName', response.data.fullName);
      navigate('/dashboard');
    } catch {
      setError('Invalid username or password');
    }
  };

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh', background: 'linear-gradient(135deg,#667EEA,#764BA2)' }}>
      <Paper elevation={4} sx={{ p: 4, borderRadius: 2, width: { xs: '90%', sm: 400 }, bgcolor: 'rgba(255,255,255,0.9)' }}>
        <Typography variant="h4" align="center" gutterBottom>Welcome Back</Typography>
        <Typography variant="body2" align="center" gutterBottom>Sign in to your account</Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <TextField
          label="Username"
          fullWidth
          margin="normal"
          value={username}
          onChange={e => setUsername(e.target.value)}
          InputProps={{ startAdornment: (<InputAdornment position="start"><Person/></InputAdornment>) }}
        />
        <TextField
          label="Password"
          fullWidth
          margin="normal"
          type={showPassword ? 'text' : 'password'}
          value={password}
          onChange={e => setPassword(e.target.value)}
          InputProps={{
            startAdornment: (<InputAdornment position="start"><Lock/></InputAdornment>),
            endAdornment: (
              <InputAdornment position="end">
                <IconButton onClick={handleTogglePassword} edge="end">
                  {showPassword ? <VisibilityOff/> : <Visibility/>}
                </IconButton>
              </InputAdornment>
            )
          }}
        />
        <Button
          fullWidth
          variant="contained"
          onClick={handleLogin}
          sx={{ mt: 2, py: 1.5, background: 'linear-gradient(45deg,#667EEA,#764BA2)' }}
        >Sign In</Button>
        <Typography align="center" sx={{ mt: 2 }}>
          Don't have an account? <MuiLink component={RouterLink} to="/register">Create Account</MuiLink>
        </Typography>
      </Paper>
    </Box>
  );
};

export default Login;