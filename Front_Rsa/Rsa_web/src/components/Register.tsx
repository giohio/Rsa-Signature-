import * as React from 'react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { Box, Paper, Typography, TextField, Button, Link as MuiLink, Alert, InputAdornment, IconButton } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import Person from '@mui/icons-material/Person';
import Lock from '@mui/icons-material/Lock';
import Mail from '@mui/icons-material/Mail';
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';

const API_URL = 'http://localhost:5000/api';

const Register: React.FC = () => {
  const [username, setUsername] = useState<string>('');
  const [password, setPassword] = useState<string>('');
  const [showPassword, setShowPassword] = useState<boolean>(false);
  const [email, setEmail] = useState<string>('');
  const [fullName, setFullName] = useState<string>('');
  const [error, setError] = useState<string>('');
  const navigate = useNavigate();

  const handleTogglePassword = () => setShowPassword(prev => !prev);

  const handleRegister = async (e: React.MouseEvent<HTMLButtonElement>) => {
    e.preventDefault();
    try {
      await axios.post(`${API_URL}/user/register`, { username, password, email, fullName });
      navigate('/login');
    } catch {
      setError('Registration failed. Username may already exist.');
    }
  };

  return (
    <Box sx={{ display:'flex', alignItems:'center', justifyContent:'center', minHeight:'100vh', background:'linear-gradient(135deg,#667EEA,#764BA2)' }}>
      <Paper elevation={4} sx={{ p:4, borderRadius:2, width:{ xs:'90%', sm:400 }, bgcolor:'rgba(255,255,255,0.9)' }}>
        <Typography variant="h4" align="center" gutterBottom>Create Account</Typography>
        <Typography variant="body2" align="center" gutterBottom>Join us today</Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <TextField
          label="Full Name"
          fullWidth
          margin="normal"
          value={fullName}
          onChange={e => setFullName(e.target.value)}
          InputProps={{ startAdornment: (<InputAdornment position="start"><Person/></InputAdornment>) }}
        />
        <TextField
          label="Username"
          fullWidth
          margin="normal"
          value={username}
          onChange={e => setUsername(e.target.value)}
          InputProps={{ startAdornment: (<InputAdornment position="start"><Person/></InputAdornment>) }}
        />
        <TextField
          label="Email"
          fullWidth
          margin="normal"
          type="email"
          value={email}
          onChange={e => setEmail(e.target.value)}
          InputProps={{ startAdornment: (<InputAdornment position="start"><Mail/></InputAdornment>) }}
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
          onClick={handleRegister}
          sx={{ mt:2, py:1.5, background:'linear-gradient(45deg,#667EEA,#764BA2)' }}
        >Create Account</Button>
        <Typography align="center" sx={{ mt:2 }}>
          Already have an account? <MuiLink component={RouterLink} to="/login">Sign In</MuiLink>
        </Typography>
      </Paper>
    </Box>
  );
};

export default Register;