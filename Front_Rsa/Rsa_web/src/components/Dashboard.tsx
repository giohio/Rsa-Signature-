import * as React from 'react';
import axios from 'axios';
import { useState, useEffect } from 'react';
import { Box, Typography, TextField, FormControl, InputLabel, Select, MenuItem, Paper, Button, Alert, Card, CardHeader, CardContent, Accordion, AccordionSummary, AccordionDetails } from '@mui/material';
import Grid from '@mui/material/GridLegacy';
import { useNavigate } from 'react-router-dom';
import DownloadIcon from '@mui/icons-material/Download';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import VerifiedUserIcon from '@mui/icons-material/VerifiedUser';
import AccountCircleIcon from '@mui/icons-material/AccountCircle';
import LogoutIcon from '@mui/icons-material/Logout';
import FingerprintIcon from '@mui/icons-material/Fingerprint';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import DescriptionIcon from '@mui/icons-material/Description';
import ManageAccountsIcon from '@mui/icons-material/ManageAccounts';

const API_URL = '/api';

interface Signature { id: string; publicKey: string; createdAt: string; }

type RawSignature = { Id: string; PublicKey: string; CreatedAt: string } | { id: string; publicKey: string; createdAt: string };

const Dashboard: React.FC = () => {
  const navigate = useNavigate();
  const [keySize, setKeySize] = useState<number>(2048);
  const [signatures, setSignatures] = useState<Signature[]>([]);
  const [signId, setSignId] = useState<string>('');
  const [selectedSignature, setSelectedSignature] = useState<Signature | null>(null);
  const [fileToSign, setFileToSign] = useState<File | null>(null);
  const [signedUrl, setSignedUrl] = useState<string>('');
  const [signError, setSignError] = useState<string>('');
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
    // Clear previous messages and state
    setMessage('');
    setVerifyResult(null);
    setSignError('');
    setSignedUrl('');
    if (!fileToSign) { setSignError('Chọn file để ký'); return; }
    if (!signId) { setSignError('Chọn chữ ký'); return; }
    try {
      const fd = new FormData();
      fd.append('UserId', localStorage.getItem('userId')!);
      fd.append('SignId', signId);
      fd.append('File', fileToSign, fileToSign.name);
      const res = await axios.post(`${API_URL}/sign/sign-document`, fd, { responseType: 'blob' });
      const url = window.URL.createObjectURL(new Blob([res.data], { type: 'application/pdf' }));
      setSignedUrl(url);
      setSignError('');
      setMessage('Document signed');
    } catch (err: unknown) {
      setSignedUrl('');
      // Dịch mã lỗi HTTP sang tiếng Việt
      let reason = 'Lỗi không xác định';
      if (axios.isAxiosError(err) && err.response) {
        const code = err.response.status;
        const reasons: Record<number,string> = {
          400: 'Yêu cầu không hợp lệ, chọn lại file đã được ký',
          401: 'Chưa đăng nhập hoặc không hợp lệ',
          403: 'Không có quyền truy cập',
          404: 'Không tìm thấy tài nguyên',
          500: 'Lỗi máy chủ',
        };
        reason = reasons[code] || `Mã lỗi ${code}`;
      }
      const errorMsg = `Ký thất bại: ${reason}`;
      setSignError(errorMsg);
      setMessage(errorMsg);
    }
  };

  const handleVerify = async () => {
    // Clear previous messages and result
    setMessage('');
    setVerifyResult(null);
    if (!fileToVerify) { setVerifyResult({ isValid: false, message: 'Chọn file để xác minh' }); return; }
    if (!signId) { setVerifyResult({ isValid: false, message: 'Chọn chữ ký' }); return; }
    try {
      const fd = new FormData();
      fd.append('UserId', localStorage.getItem('userId')!);
      fd.append('SignId', signId);
      fd.append('File', fileToVerify, fileToVerify.name);
      const res = await axios.post(`${API_URL}/sign/verify-signature`, fd);
      const { isValid, message, fullName, email } = res.data;
      setVerifyResult({ isValid, message, fullName, email });
    } catch (err: unknown) {
      // Dịch mã lỗi HTTP sang tiếng Việt
      let reason = 'Lỗi không xác định';
      if (axios.isAxiosError(err) && err.response) {
        const code = err.response.status;
        const reasons: Record<number,string> = {
          400: 'Yêu cầu không hợp lệ',
          401: 'Chưa đăng nhập hoặc không hợp lệ',
          403: 'Không có quyền truy cập',
          404: 'Không tìm thấy tài nguyên',
          500: 'Lỗi máy chủ',
        };
        reason = reasons[code] || `Mã lỗi ${code}`;
      }
      const errorMsg = `Xác thực thất bại: ${reason}`;
      setVerifyResult({ isValid: false, message: errorMsg });
    }
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
        {message && (
          <Alert severity="info" sx={{ mb: 2 }}>
            {message}
          </Alert>
        )}

        <Grid container spacing={3} sx={{ mb: 4 }}>
          <Grid item xs={12} md={4}>
            <Paper sx={{ p: 3, height: '100%', display: 'flex', flexDirection: 'column' }}>
              <Typography variant="h6" gutterBottom sx={{ display: 'flex', alignItems: 'center' }}>
                <DescriptionIcon sx={{ mr: 1 }} />
                Ký văn bản
              </Typography>
              <Typography variant="body2" sx={{ mb: 2 }}>
                Ký tài liệu với chữ ký số của bạn
              </Typography>
              <Button 
                variant="contained" 
                color="primary"
                onClick={() => navigate('/sign_file')}
                sx={{ mt: 'auto' }}
              >
                Đi đến trang ký văn bản
              </Button>
            </Paper>
          </Grid>
          
          <Grid item xs={12} md={4}>
            <Paper sx={{ p: 3, height: '100%', display: 'flex', flexDirection: 'column' }}>
              <Typography variant="h6" gutterBottom sx={{ display: 'flex', alignItems: 'center' }}>
                <VerifiedUserIcon sx={{ mr: 1 }} />
                Xác thực văn bản
              </Typography>
              <Typography variant="body2" sx={{ mb: 2 }}>
                Xác thực tính hợp lệ của chữ ký số trên tài liệu
              </Typography>
              <Button 
                variant="contained" 
                color="secondary"
                onClick={() => navigate('/verify_file')}
                sx={{ mt: 'auto' }}
              >
                Đi đến trang xác thực
              </Button>
            </Paper>
          </Grid>
          
          <Grid item xs={12} md={4}>
            <Paper sx={{ p: 3, height: '100%', display: 'flex', flexDirection: 'column' }}>
              <Typography variant="h6" gutterBottom sx={{ display: 'flex', alignItems: 'center' }}>
                <ManageAccountsIcon sx={{ mr: 1 }} />
                Quản lý chữ ký
              </Typography>
              <Typography variant="body2" sx={{ mb: 2 }}>
                Tạo, chỉnh sửa và quản lý các chữ ký số của bạn
              </Typography>
              <Button 
                variant="contained" 
                color="info"
                onClick={() => navigate('/signatures')}
                sx={{ mt: 'auto' }}
              >
                Đi đến quản lý chữ ký
              </Button>
            </Paper>
          </Grid>
        </Grid>

        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(3,1fr)' }, gap: 4 }}>
          <Card variant="outlined">
            <CardHeader avatar={<EditIcon color="primary" />} title="Ký tài liệu" titleTypographyProps={{ variant: 'h6' }} />
            <CardContent>
              <TextField type="file" fullWidth sx={{ mt: 1 }} onChange={(e: React.ChangeEvent<HTMLInputElement>) => setFileToSign(e.target.files?.[0] ?? null)} InputLabelProps={{ shrink: true }} />
              <Button variant="contained" fullWidth sx={{ mt: 2 }} onClick={handleSign}>KÝ</Button>
              {signedUrl && (
                <>
                  <Alert severity="success" sx={{ mt: 2 }}>Ký thành công</Alert>
                  <Button startIcon={<DownloadIcon />} href={signedUrl} download fullWidth sx={{ mt: 2 }}>TẢI XUỐNG</Button>
                </>
              )}
              {!signedUrl && signError && (
                <Alert severity="error" sx={{ mt: 2 }}>{signError}</Alert>
              )}
            </CardContent>
          </Card>
          <Card variant="outlined">
            <CardHeader avatar={<VerifiedUserIcon color="primary" />} title="Xác minh chữ ký" titleTypographyProps={{ variant: 'h6' }} />
            <CardContent>
              <TextField type="file" fullWidth sx={{ mt: 1 }} onChange={(e: React.ChangeEvent<HTMLInputElement>) => setFileToVerify(e.target.files?.[0] ?? null)} InputLabelProps={{ shrink: true }} />
              <Button variant="contained" fullWidth sx={{ mt: 2 }} onClick={handleVerify}>XÁC MINH</Button>
              {verifyResult && verifyResult.isValid && (
                <Alert severity="success" sx={{ mt: 2 }}>Xác thực thành công</Alert>
              )}
              {verifyResult && verifyResult.isValid && (
                <Card variant="outlined" sx={{ mt: 2, borderColor: 'success.main' }}>
                  <CardHeader avatar={<VerifiedUserIcon color="success" />} title="Chữ ký hợp lệ" titleTypographyProps={{ variant: 'h6' }} />
                  {verifyResult.fullName && (
                    <CardContent>
                      <Typography variant="subtitle2">Thông tin người ký</Typography>
                      <Typography variant="body2">Họ và tên: {verifyResult.fullName}</Typography>
                      <Typography variant="body2">Email: {verifyResult.email}</Typography>
                    </CardContent>
                  )}
                </Card>
              )}
              {verifyResult && !verifyResult.isValid && (
                <>
                  <Alert severity="error" sx={{ mt: 2 }}>Xác thực thất bại</Alert>
                  <Typography color="error" sx={{ mt: 1 }}>{verifyResult.message}</Typography>
                </>
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