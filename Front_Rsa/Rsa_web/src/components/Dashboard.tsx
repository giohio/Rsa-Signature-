import * as React from 'react';
import axios from 'axios';
import { useState, useEffect } from 'react';
import { Box, Typography, TextField, FormControl, InputLabel, Select, MenuItem, Paper, Button, Alert, Card, CardHeader, CardContent, Accordion, AccordionSummary, AccordionDetails } from '@mui/material';
import DownloadIcon from '@mui/icons-material/Download';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import VerifiedUserIcon from '@mui/icons-material/VerifiedUser';
import AccountCircleIcon from '@mui/icons-material/AccountCircle';
import LogoutIcon from '@mui/icons-material/Logout';
import FingerprintIcon from '@mui/icons-material/Fingerprint';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';

const API_URL = 'http://localhost:5000/api';

interface Signature { id: string; publicKey: string; createdAt: string; }

type RawSignature = { Id: string; PublicKey: string; CreatedAt: string } | { id: string; publicKey: string; createdAt: string };

const Dashboard: React.FC = () => {
  const [keySize, setKeySize] = useState<number>(2048);
  const [signatures, setSignatures] = useState<Signature[]>([]);
  const [signId, setSignId] = useState<string>('');
  const [selectedSignature, setSelectedSignature] = useState<Signature | null>(null);
  const [fileToSign, setFileToSign] = useState<File | null>(null);
  const [signedUrl, setSignedUrl] = useState<string>('');
  const [fileToVerify, setFileToVerify] = useState<File | null>(null);
  const [verifyResult, setVerifyResult] = useState<{ isValid: boolean; message: string; fullName?: string; email?: string } | null>(null);
  const [message, setMessage] = useState<string>('');
  const fullName = localStorage.getItem('fullName') || 'User';
  const truncateKey = (key: string, head = 8, tail = 8): string =>
    key.length > head + tail ? `${key.slice(0, head)}...${key.slice(-tail)}` : key;

  useEffect(() => {
    const fetchSignatures = async () => {
      try {
        const res = await axios.get<{ signatures: RawSignature[] }>(`${API_URL}/sign/list/${localStorage.getItem('userId')}`);
        const norm = res.data.signatures.map(s => ({
          id: 'id' in s ? s.id : s.Id,
          publicKey: 'publicKey' in s ? s.publicKey : s.PublicKey,
          createdAt: 'createdAt' in s ? s.createdAt : s.CreatedAt,
        }));
        setSignatures(norm);
      } catch { setMessage('Failed to load signatures'); }
    };
    fetchSignatures();
  }, []);

  useEffect(() => { if (!localStorage.getItem('userId')) window.location.href = '/login'; }, []);
  useEffect(() => { setSelectedSignature(signatures.find(s => s.id === signId) || null); }, [signId, signatures]);

  const handleGenerate = async () => {
    try {
      const res = await axios.post(`${API_URL}/sign/generate-keys`, { userId: localStorage.getItem('userId'), keySize });
      setSignId(res.data.signId);
      setSignatures(prev => [...prev, { id: res.data.signId, publicKey: res.data.publicKey, createdAt: new Date().toISOString() }]);
      setMessage('Key generated');
    } catch { setMessage('Generate failed'); }
  };

  const handleSign = async () => {
    if (!fileToSign || !signId) { setMessage('Select file + key'); return; }
    try {
      const fd = new FormData();
      fd.append('UserId', localStorage.getItem('userId')!);
      fd.append('SignId', signId);
      fd.append('File', fileToSign, fileToSign.name);
      const res = await axios.post(`${API_URL}/sign/sign-document`, fd, { responseType: 'blob' });
      const url = window.URL.createObjectURL(new Blob([res.data], { type: 'application/pdf' }));
      setSignedUrl(url);
      setMessage('Document signed');
    } catch { setMessage('Sign failed'); }
  };

  const handleVerify = async () => {
    if (!fileToVerify || !signId) { setMessage('Select file + key'); return; }
    try {
      const fd = new FormData();
      fd.append('UserId', localStorage.getItem('userId')!);
      fd.append('SignId', signId);
      fd.append('File', fileToVerify, fileToVerify.name);
      const res = await axios.post(`${API_URL}/sign/verify-signature`, fd);
      const { isValid, message, fullName, email } = res.data;
      setVerifyResult({ isValid, message, fullName, email });
    } catch { setMessage('Verify failed'); }
  };

  const handleDelete = async (id: string) => {
    try {
      const userId = localStorage.getItem('userId');
      if (!userId) throw new Error('No userId');
      await axios.delete(`${API_URL}/sign/delete/${userId}/${id}`);
      setSignatures(prev => prev.filter(s => s.id !== id));
      if (signId === id) setSignId('');
      setMessage('Signature deleted');
    } catch (err: unknown) {
      console.error('Delete error:', err);
      let errMsg = 'Unknown error';
      if (axios.isAxiosError(err)) {
        errMsg = err.response?.data?.message || err.message;
      } else if (err instanceof Error) {
        errMsg = err.message;
      }
      setMessage(`Delete failed: ${errMsg}`);
    }
  };

  const handleLogout = () => { localStorage.removeItem('userId'); window.location.href = '/login'; };

  return (
    <Box sx={{ display: 'flex', height: '100vh' }}>
      <Box component="nav" sx={{ width: 300, flexShrink: 0, bgcolor: 'primary.dark', color: 'white', p: 2, display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Typography variant="h6" sx={{ mb: 1, display: 'flex', alignItems: 'center' }}>
          <FingerprintIcon sx={{ mr: 1 }} />
          Digital Signature
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
          <AccountCircleIcon sx={{ fontSize: 40 }} />
          <Typography variant="subtitle1" sx={{ ml: 1 }}>Hello, {fullName}</Typography>
        </Box>
        <Button startIcon={<LogoutIcon />} variant="contained" color="error" fullWidth onClick={handleLogout} sx={{ mb: 2 }}>
          Logout
        </Button>
        <Paper variant="outlined" sx={{ bgcolor: 'primary.main', p: 2, color: 'white' }}>
          <Typography variant="subtitle2" gutterBottom>Quản lý khóa</Typography>
          <FormControl fullWidth variant="filled" sx={{ mb: 1, bgcolor: 'primary.dark', borderRadius: 1 }}>
            <InputLabel sx={{ color: 'white' }}>Kích thước khóa</InputLabel>
            <Select value={keySize} onChange={e => setKeySize(Number(e.target.value))} sx={{ color: 'white' }}>
              <MenuItem value={2048}>2048</MenuItem>
              <MenuItem value={3072}>3072</MenuItem>
              <MenuItem value={4096}>4096</MenuItem>
            </Select>
          </FormControl>
          <Button variant="contained" fullWidth onClick={handleGenerate}>Tạo khóa</Button>
        </Paper>
        <Paper variant="outlined" sx={{ bgcolor: 'primary.dark', p: 2, color: 'white' }}>
          <Typography variant="subtitle2" gutterBottom>Chọn chữ ký</Typography>
          <FormControl fullWidth variant="filled">
            <InputLabel sx={{ color: 'white' }}>Chữ ký</InputLabel>
            <Select value={signId} onChange={e => setSignId(e.target.value)} sx={{ color: 'white' }}>
              <MenuItem value="">Chọn</MenuItem>
              {signatures.map((s, idx) => <MenuItem key={s.id} value={s.id}>Khóa {idx + 1}</MenuItem>)}
            </Select>
          </FormControl>
        </Paper>
        {selectedSignature && (
          <Box sx={{ bgcolor: 'primary.dark', p: 1, mt: 1, borderRadius: 1 }}>
            <Typography variant="body2">ID: {selectedSignature.id}</Typography>
            <Typography variant="body2">Tạo lúc: {new Date(selectedSignature.createdAt).toLocaleString()}</Typography>
          </Box>
        )}
      </Box>
      <Box component="main" sx={{ flexGrow: 1, bgcolor: 'grey.100', p: 4, overflow: 'auto' }}>
        {message && <Alert severity="info" sx={{ mb: 2 }}>{message}</Alert>}
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(3,1fr)' }, gap: 4 }}>
          <Card variant="outlined">
            <CardHeader avatar={<EditIcon color="primary" />} title="Ký tài liệu" titleTypographyProps={{ variant: 'h6' }} />
            <CardContent>
              <TextField type="file" fullWidth sx={{ mt: 1 }} onChange={(e: React.ChangeEvent<HTMLInputElement>) => setFileToSign(e.target.files?.[0] ?? null)} InputLabelProps={{ shrink: true }} />
              <Button variant="contained" fullWidth sx={{ mt: 2 }} onClick={handleSign}>KÝ</Button>
              {signedUrl && <Button startIcon={<DownloadIcon />} href={signedUrl} download fullWidth sx={{ mt: 2 }}>TẢI XUỐNG</Button>}
            </CardContent>
          </Card>
          <Card variant="outlined">
            <CardHeader avatar={<VerifiedUserIcon color="primary" />} title="Xác minh chữ ký" titleTypographyProps={{ variant: 'h6' }} />
            <CardContent>
              <TextField type="file" fullWidth sx={{ mt: 1 }} onChange={(e: React.ChangeEvent<HTMLInputElement>) => setFileToVerify(e.target.files?.[0] ?? null)} InputLabelProps={{ shrink: true }} />
              <Button variant="contained" fullWidth sx={{ mt: 2 }} onClick={handleVerify}>XÁC MINH</Button>
              {verifyResult && (
                <Card variant="outlined" sx={{ mt: 2, borderColor: verifyResult.isValid ? 'success.main' : 'error.main' }}>
                  <CardHeader avatar={<VerifiedUserIcon color={verifyResult.isValid ? 'success' : 'error'} />} title={verifyResult.isValid ? 'Chữ ký hợp lệ' : verifyResult.message} titleTypographyProps={{ variant: 'h6' }} />
                  {verifyResult.fullName && (
                    <CardContent>
                      <Typography variant="subtitle2">Thông tin người ký</Typography>
                      <Typography variant="body2">Họ và tên: {verifyResult.fullName}</Typography>
                      <Typography variant="body2">Email: {verifyResult.email}</Typography>
                    </CardContent>
                  )}
                </Card>
              )}
            </CardContent>
          </Card>
          <Box sx={{ gridColumn: { xs: '1/-1', sm: 'auto' } }}>
            <Typography variant="h6" gutterBottom>Thông tin chữ ký</Typography>
            {signatures.map((s, idx) => (
              <Accordion key={s.id} expanded={signId === s.id} onChange={(_, expanded: boolean) => setSignId(expanded ? s.id : '')}>
                <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                  <Typography>Chữ ký {idx + 1}</Typography>
                </AccordionSummary>
                <AccordionDetails>
                  <Typography variant="caption">Khóa công khai: {truncateKey(s.publicKey)}</Typography>
                  <Typography variant="body2">Tạo lúc: {new Date(s.createdAt).toLocaleString()}</Typography>
                  <Button startIcon={<DeleteIcon />} color="error" onClick={e => { e.stopPropagation(); handleDelete(s.id); }}>
                    Xóa
                  </Button>
                </AccordionDetails>
              </Accordion>
            ))}
          </Box>
        </Box>
      </Box>
    </Box>
  );
};

export default Dashboard;