import React, { useState, useRef, useEffect } from 'react';
import axios from 'axios';
import { useNavigate } from 'react-router-dom';
import {
  Box, Button, Paper, Typography, TextField, Container,
  AppBar, Toolbar, IconButton, CircularProgress,
  Card, CardContent, Alert, AlertTitle,
  FormControl, InputLabel, Select, MenuItem, Tabs, Tab,
  Chip, Stepper, Step, StepLabel
} from '@mui/material';
import FileUploadIcon from '@mui/icons-material/FileUpload';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CancelIcon from '@mui/icons-material/Cancel';
import LinkIcon from '@mui/icons-material/Link';
import MenuIcon from '@mui/icons-material/Menu';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import TextFieldsIcon from '@mui/icons-material/TextFields';
import VerifiedUserIcon from '@mui/icons-material/VerifiedUser';
import KeyIcon from '@mui/icons-material/Key';
import DescriptionIcon from '@mui/icons-material/Description';
import CreateIcon from '@mui/icons-material/Create';
import SettingsIcon from '@mui/icons-material/Settings';
import { useSnackbar } from 'notistack';

const API_URL = '/api';

interface Signature {
  id: string;
  publicKey: string;
  createdAt: string;
  signatureName: string;
  signatureType: string;
  isActive: boolean;
}

interface TabPanelProps {
  children?: React.ReactNode;
  index: number;
  value: number;
}

// Interface VerificationResult (cập nhật để thêm fullName và email)

interface VerificationResult {
  isValid: boolean;
  message: string;
  tamperedDetected?: boolean;
  fullName?: string; // Thêm để khắc phục lỗi TypeScript 2339
  email?: string; // Thêm để khắc phục lỗi TypeScript 2339
  metadata?: {
    fileName?: string;
    fileSize?: number;
    timestamp?: string;
    hashAlgorithm?: string;
    originalHash?: string;
    currentHash?: string;
  };
}

function TabPanel(props: TabPanelProps) {
  const { children, value, index, ...other } = props;

  return (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`tabpanel-${index}`}
      aria-labelledby={`tab-${index}`}
      {...other}
    >
      {value === index && (
        <Box sx={{ pt: 2 }}>
          {children}
        </Box>
      )}
    </div>
  );
}

// Validate Base64 string
const isValidBase64 = (str: string): boolean => {
  if (!str) return false;
  
  // Remove any whitespace
  const trimmed = str.replace(/\s/g, '');
  
  // Check if it's a valid base64 format
  // Base64 should only contain A-Z, a-z, 0-9, +, /, and = (padding)
  const base64Regex = /^[A-Za-z0-9+/]+(=|==)?$/;
  return base64Regex.test(trimmed);
};

// Clean Base64 string
const cleanBase64 = (str: string): string => {
  // Remove whitespace, line breaks, and any non-Base64 characters
  return str.replace(/\s/g, '').replace(/[^A-Za-z0-9+/=]/g, '');
};

const Verify_file: React.FC = () => {
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const publicKeyFileInputRef = useRef<HTMLInputElement>(null);
  const signatureFileInputRef = useRef<HTMLInputElement>(null);
  const { enqueueSnackbar } = useSnackbar();
  
  // Tab state
  const [tabValue, setTabValue] = useState<number>(0);
  
  // States for file upload
  const [fileName, setFileName] = useState<string>('');
  const [fileToVerify, setFileToVerify] = useState<File | null>(null);
  const [fileSize, setFileSize] = useState<string>('');
  const [filePreview, setFilePreview] = useState<string>('');
  const [filePreviewType, setFilePreviewType] = useState<'text' | 'pdf' | 'image' | 'office' | 'unsupported' | ''>('');
  
  // State for text input
  const [textContent, setTextContent] = useState<string>('');
  
  // States for verification
  const [isVerifying, setIsVerifying] = useState<boolean>(false);
  const [verificationResult, setVerificationResult] = useState<VerificationResult | null>(null);
  
  // States for public key
  const [publicKey, setPublicKey] = useState<string>('');
  const [selectedSignatureId, setSelectedSignatureId] = useState<string>('');
  const [signatures, setSignatures] = useState<Signature[]>([]);
  
  // States for signature
  const [signature, setSignature] = useState<string>('');
  const [signatureError, setSignatureError] = useState<string | null>(null);
  const [signatureFileName, setSignatureFileName] = useState<string>('');
  const [signatureSource, setSignatureSource] = useState<'paste' | 'file'>('paste');
  const [hashAlgorithm, setHashAlgorithm] = useState<string>('SHA256');
  
  // Error state
  const [error, setError] = useState<string | null>(null);

  // Add these state variables
  const [publicKeySource, setPublicKeySource] = useState<'select' | 'file'>('select');
  const [publicKeyFileName, setPublicKeyFileName] = useState<string>('');

  // Add isEmbedded state
  const [isEmbedded, setIsEmbedded] = useState<boolean>(true);

  // Handle tab change
  const handleTabChange = (event: React.SyntheticEvent, newValue: number) => {
    setTabValue(newValue);
    setVerificationResult(null);
    setError(null);
    setFilePreview('');
    setFilePreviewType('');
  };

  // Fetch signatures on component mount
  useEffect(() => {
    const userId = localStorage.getItem('userId');
    if (userId) {
      fetchSignatures(userId);
    }
  }, []);

  // Validate signature when it changes
  useEffect(() => {
    if (signature) {
      validateSignature(signature);
    } else {
      setSignatureError(null);
    }
  }, [signature]);

  // Fetch available signatures
  const fetchSignatures = async (userId: string) => {
    try {
      const response = await axios.get<{ signatures: Signature[] }>(`${API_URL}/sign/list/${userId}`);
      setSignatures(response.data.signatures);
      if (response.data.signatures.length > 0) {
        setSelectedSignatureId(response.data.signatures[0].id);
      }
    } catch (error) {
      console.error('Error fetching signatures:', error);
      showNotification('Failed to fetch signatures', 'error');
    }
  };

  // Validate signature
  const validateSignature = (sig: string): boolean => {
    if (!sig.trim()) {
      setSignatureError('Chữ ký không được để trống');
      return false;
    }
    
    if (!isValidBase64(sig.trim())) {
      setSignatureError('Chữ ký không đúng định dạng Base64');
      return false;
    }
    
    setSignatureError(null);
    return true;
  };

  // Handle signature input change
  const handleSignatureChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setSignature(value);
  };

  // Handle signature paste
  const handleSignaturePaste = (e: React.ClipboardEvent<HTMLDivElement>) => {
    const pastedText = e.clipboardData.getData('text');
    
    // If it looks like Base64 but might have extra characters, try to clean it
    if (pastedText.length > 20 && pastedText.match(/[A-Za-z0-9+/=]/)) {
      const cleaned = cleanBase64(pastedText);
      if (cleaned !== pastedText) {
        e.preventDefault();
        setSignature(cleaned);
        showNotification('Chữ ký đã được định dạng lại để phù hợp với Base64', 'info');
      }
    }
  };

  // Handle file selection
  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    if (event.target.files && event.target.files.length > 0) {
      const file = event.target.files[0];
      setFileToVerify(file);
      setFileName(file.name);
      setFileSize(`${(file.size / 1024).toFixed(2)} KB`);
      setVerificationResult(null); // Reset previous results
      setError(null); // Reset error
      setFilePreview(''); // Clear previous preview
      setFilePreviewType(''); // Reset preview type
      
      // For file preview
      if (file.type.startsWith('text/') || 
          ['.txt', '.json', '.xml', '.html', '.md', '.csv'].some(ext => 
            file.name.toLowerCase().endsWith(ext))) {
        // Text files
        const reader = new FileReader();
        reader.onload = (e) => {
          const content = e.target?.result as string;
          setFilePreview(content);
          setFilePreviewType('text');
        };
        reader.onerror = () => {
          showNotification('Lỗi khi đọc nội dung file', 'error');
        };
        reader.readAsText(file);
      } else if (file.type.startsWith('application/pdf')) {
        // PDF files - create object URL for iframe preview
        const objectUrl = URL.createObjectURL(file);
        setFilePreview(objectUrl);
        setFilePreviewType('pdf');
      } else if (file.type.startsWith('image/')) {
        // Image files
        const reader = new FileReader();
        reader.onload = (e) => {
          const content = e.target?.result as string;
          setFilePreview(content);
          setFilePreviewType('image');
        };
        reader.readAsDataURL(file);
      } else if (['.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx'].some(ext => 
                  file.name.toLowerCase().endsWith(ext))) {
        // Office files - use Google Docs Viewer
        // Note: This requires the file to be publicly accessible, which isn't the case for local files
        // This is just to demonstrate the concept - in practice we'd need to upload to a temp location
        setFilePreview(`https://docs.google.com/viewer?url=NEED_PUBLIC_URL_HERE&embedded=true`);
        setFilePreviewType('office');
        
        // Since we can't actually preview Office files without uploading them first,
        // we'll just show a message
        setFilePreviewType('unsupported');
        setFilePreview(`File Office (${file.name.split('.').pop()?.toUpperCase()}) được chọn - cần tích hợp API để xem trước`);
      } else {
        // Unsupported file types
        setFilePreviewType('unsupported');
        setFilePreview(`Tập tin ${file.type || file.name.split('.').pop()?.toUpperCase() || 'không xác định'} - xem trước không khả dụng`);
      }
    }
  };

  // Handle signature selection
  const handleSignatureChange_Select = async (event: React.ChangeEvent<{ value: unknown }>) => {
    const signId = event.target.value as string;
    setSelectedSignatureId(signId);
    
    if (signId) {
      try {
        const userId = localStorage.getItem('userId');
        const response = await axios.get(`${API_URL}/manualsign/get-key-details/${userId}/${signId}`);
        
        if (response.data && response.data.signature && response.data.signature.publicKey) {
          setPublicKey(response.data.signature.publicKey);
        }
      } catch (error) {
        console.error('Error fetching public key:', error);
        showNotification('Failed to fetch public key', 'error');
      }
    }
  };

  
  // Handle signature file selection
  const handleSignatureFileSelect = async (event: React.ChangeEvent<HTMLInputElement>) => {
    if (event.target.files && event.target.files.length > 0) {
      const file = event.target.files[0];
      setSignatureFileName(file.name);
      try {
        const reader = new FileReader();
        reader.onload = (e) => {
          try {
            const content = e.target?.result as string;
            // For .sig, .txt, .json: just use the raw content (no JSON parsing)
            setSignature(content.trim());
            showNotification('Chữ ký đã được tải thành công từ file', 'success');
          } catch (error) {
            showNotification('Không thể đọc file chữ ký', 'error');
          }
        };
        reader.onerror = () => {
          showNotification('Lỗi khi đọc file', 'error');
        };
        reader.readAsText(file);
      } catch (error) {
        showNotification('Không thể đọc file chữ ký', 'error');
      }
    }
  };

  // Create file from text content
  const createFileFromText = (): File => {
    const blob = new Blob([textContent], { type: 'text/plain' });
    return new File([blob], 'text-content.txt', { type: 'text/plain' });
  };

  // Handle embedded signature toggle
  const handleEmbeddedToggle = (_event: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = !isEmbedded;
    setIsEmbedded(newValue);
    // Reset to file tab when switching to embedded mode
    if (newValue && tabValue !== 0) {
      setTabValue(0);
    }
  };

  // Update the handleVerify function to use the embedded API
  const handleVerify = async () => {
    let fileForVerification: File | null = null;
    
    if (tabValue === 0) {
      // File tab
      if (!fileToVerify) {
        showNotification('Vui lòng chọn tập tin để xác thực', 'error');
        return;
      }
      fileForVerification = fileToVerify;
    } else {
      // Text tab
      if (!textContent.trim()) {
        showNotification('Vui lòng nhập nội dung văn bản để xác thực', 'error');
        return;
      }
      fileForVerification = createFileFromText();
    }
    
    // Validate inputs based on verification mode
    if (isEmbedded) {
      // For embedded signatures, we only need the file
      if (!fileForVerification) {
        setError('Vui lòng chọn tập tin để xác thực');
        showNotification('Vui lòng chọn tập tin để xác thực', 'error');
        return;
      }
    } else {
      // For detached signatures, we need both file and signature
      if (!signature) {
        setError('Vui lòng nhập chữ ký để xác thực');
        showNotification('Vui lòng nhập chữ ký để xác thực', 'error');
        return;
      }
      
      if (signatureError) {
        setError(`Chữ ký không hợp lệ: ${signatureError}`);
        showNotification(`Chữ ký không hợp lệ: ${signatureError}`, 'error');
        return;
      }
      
      if (!publicKey) {
        setError('Vui lòng chọn khóa công khai để xác thực');
        showNotification('Vui lòng chọn khóa công khai để xác thực', 'error');
        return;
      }
    }
    
    setIsVerifying(true);
    setError(null);
    
    try {
      if (isEmbedded) {
        // Use the embedded signature verification API
        const formData = new FormData();
        formData.append('file', fileForVerification);
        formData.append('isembedded', 'true');
        
        const response = await axios.post(`${API_URL}/sign/verify-signature`, formData, {
          headers: {
            'Content-Type': 'multipart/form-data'
          }
        });
        
        setVerificationResult(response.data);
        
        if (response.data.isValid) {
          showNotification('Xác thực chữ ký thành công', 'success');
        } else {
          showNotification(response.data.message || 'Xác thực chữ ký thất bại', 'warning');
        }
      } else {
        // Use the detached signature verification API
        // Clean signature before sending
        const cleanedSignature = cleanBase64(signature);
        
        // Create form data for file upload
        const formData = new FormData();
        formData.append('file', fileForVerification);
        formData.append('signature', cleanedSignature);
        formData.append('publicKey', publicKey);
        formData.append('hashAlgorithm', hashAlgorithm);
        
        const response = await axios.post(`${API_URL}/manualsign/verify-file-signature`, formData, {
          headers: {
            'Content-Type': 'multipart/form-data'
          }
        });
        
        setVerificationResult(response.data);
        
        if (response.data.isValid) {
          showNotification('Xác thực chữ ký thành công', 'success');
        } else {
          // Check for specific tampering errors
          const errorMsg = response.data.message || '';
          if (errorMsg.toLowerCase().includes('hash') || 
              errorMsg.toLowerCase().includes('băm') ||
              errorMsg.toLowerCase().includes('digest') ||
              errorMsg.toLowerCase().includes('checksum') ||
              errorMsg.toLowerCase().includes('thay đổi') ||
              errorMsg.toLowerCase().includes('sửa đổi') ||
              errorMsg.toLowerCase().includes('tampered')) {
            // Mark result as tampered
            response.data.tamperedDetected = true;
            
            // Try to extract hash information if available
            const originalHashMatch = errorMsg.match(/original hash:?\s*([a-zA-Z0-9+/=]+)/i);
            const currentHashMatch = errorMsg.match(/current hash:?\s*([a-zA-Z0-9+/=]+)/i);
            
            if (originalHashMatch && currentHashMatch && response.data.metadata) {
              response.data.metadata.originalHash = originalHashMatch[1];
              response.data.metadata.currentHash = currentHashMatch[1];
            }
            
            showNotification('Phát hiện văn bản đã bị chỉnh sửa sau khi ký!', 'error');
          } else if (errorMsg.toLowerCase().includes('chữ ký không hợp lệ') || 
                    errorMsg.toLowerCase().includes('invalid signature')) {
            // This is a case where the signature itself was modified
            response.data.tamperedDetected = true;
            showNotification('Chữ ký đã bị thay đổi hoặc không khớp với văn bản!', 'error');
          } else {
            showNotification(errorMsg || 'Xác thực chữ ký thất bại', 'warning');
          }
        }
      }
    } catch (error) {
      console.error('Error verifying file:', error);
      setVerificationResult(null);
      
      if (axios.isAxiosError(error) && error.response) {
        const errorMessage = error.response.data?.message || error.response.data?.error || error.message;
        
        // Check for specific Base64 error
        if (errorMessage.includes('not a valid Base-64 string')) {
          setError('Chữ ký không đúng định dạng Base64. Vui lòng kiểm tra lại.');
          showNotification('Chữ ký không đúng định dạng Base64', 'error');
        } else {
          setError(`Lỗi khi xác thực tập tin: ${errorMessage}`);
          showNotification(`Lỗi: ${errorMessage}`, 'error');
        }
      } else {
        setError('Lỗi không xác định khi xác thực tập tin');
        showNotification('Lỗi khi xác thực tập tin', 'error');
      }
    } finally {
      setIsVerifying(false);
    }
  };

  // Show notification
  const showNotification = (message: string, variant: 'success' | 'error' | 'warning' | 'info') => {
    enqueueSnackbar(message, { variant });
  };

  // Go back to dashboard
  const goToDashboard = () => {
    navigate('/homepage');
  };

  // Add this handler for public key file selection
  const handlePublicKeyFileSelect = async (event: React.ChangeEvent<HTMLInputElement>) => {
    if (event.target.files && event.target.files.length > 0) {
      const file = event.target.files[0];
      setPublicKeyFileName(file.name);
      try {
        const reader = new FileReader();
        reader.onload = (e) => {
          try {
            const content = e.target?.result as string;
            // For .pem, .txt: use raw content. For .json, try to extract publicKey field, else use raw.
            if (file.name.endsWith('.json')) {
              try {
                const jsonData = JSON.parse(content);
                if (jsonData.publicKey) {
                  setPublicKey(jsonData.publicKey);
                  showNotification('Khóa công khai đã được tải thành công từ file JSON', 'success');
                  return;
                }
              } catch {}
            }
            setPublicKey(content.trim());
            showNotification('Khóa công khai đã được tải thành công từ file', 'success');
          } catch (error) {
            showNotification('Không thể đọc file khóa công khai', 'error');
          }
        };
        reader.onerror = () => {
          showNotification('Lỗi khi đọc file khóa công khai', 'error');
        };
        reader.readAsText(file);
      } catch (error) {
        showNotification('Không thể đọc file khóa công khai', 'error');
      }
    }
  };

  // Hàm để hiển thị nội dung chữ ký dạng rút gọn
  const getSignaturePreview = (sig: string): string => {
    if (!sig) return '';
    if (sig.length <= 100) return sig;
    return sig.substring(0, 50) + '...' + sig.substring(sig.length - 50);
  };

  // Hàm để hiển thị nội dung khóa công khai dạng rút gọn
  const getPublicKeyPreview = (key: string): string => {
    if (!key) return '';
    
    // Nếu là JSON, hiển thị dạng đẹp hơn
    try {
      const jsonObj = JSON.parse(key);
      return JSON.stringify(jsonObj, null, 2).substring(0, 200) + (JSON.stringify(jsonObj, null, 2).length > 200 ? '...' : '');
    } catch {
      // Nếu không phải JSON, hiển thị dạng rút gọn
      if (key.length <= 100) return key;
      return key.substring(0, 50) + '...' + key.substring(key.length - 50);
    }
  };

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: '#f5f5f5' }}>
      <AppBar position="static" elevation={0} sx={{ bgcolor: 'primary.main' }}>
        <Toolbar>
          <LinkIcon sx={{ mr: 1 }} />
          <Typography 
            variant="h6" 
            component="div" 
            sx={{ flexGrow: 1, cursor: 'pointer', fontWeight: 'bold' }} 
            onClick={goToDashboard}
          >
            RSA DIGITAL SIGNATURE
          </Typography>
          <IconButton edge="end" color="inherit">
            <MenuIcon />
          </IconButton>
        </Toolbar>
      </AppBar>

      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Paper sx={{ p: { xs: 2, md: 4 }, borderRadius: 2, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
            <VerifiedUserIcon sx={{ fontSize: 32, color: 'primary.main', mr: 2 }} />
            <Typography variant="h4" fontWeight="bold">
              Xác thực tài liệu
            </Typography>
          </Box>
          
          <Stepper activeStep={1} alternativeLabel sx={{ mb: 4, display: { xs: 'none', md: 'flex' } }}>
            <Step completed={!!signature}>
              <StepLabel icon={<KeyIcon />}>Chọn chữ ký</StepLabel>
            </Step>
            <Step completed={tabValue === 0 ? !!fileToVerify : !!textContent}>
              <StepLabel icon={<DescriptionIcon />}>Chọn văn bản</StepLabel>
            </Step>
            <Step completed={!!verificationResult}>
              <StepLabel icon={<VerifiedUserIcon />}>Xác thực</StepLabel>
            </Step>
          </Stepper>
          
          {error && (
            <Alert severity="error" sx={{ mb: 3, borderRadius: 2 }}>
              <AlertTitle>Lỗi</AlertTitle>
              {error}
            </Alert>
          )}
          
          {/* Step 1: Verification Mode and Signature Inputs */}
          <Paper 
            elevation={2} 
            sx={{ 
              p: { xs: 2, md: 3 }, 
              mb: 3, 
              borderRadius: 2,
              bgcolor: '#ffffff'
            }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
              <KeyIcon sx={{ color: 'primary.main', mr: 2, fontSize: 24 }} />
              <Typography variant="h6" fontWeight="bold">
                Bước 1: Chọn chế độ xác thực và nhập thông tin
              </Typography>
            </Box>

            <Box sx={{ mb: 3 }}>
              <Typography variant="subtitle1" fontWeight="bold" sx={{ mb: 2 }}>
                Chế độ xác thực
              </Typography>
              <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
                {[
                  { value: true, label: 'Chữ ký nhúng', description: 'Cho file PDF có chữ ký số nhúng', icon: VerifiedUserIcon },
                  { value: false, label: 'Chữ ký tách rời', description: 'Cho file có chữ ký tách rời (.sig)', icon: LinkIcon }
                ].map((mode) => (
                  <Paper
                    key={mode.label}
                    onClick={() => {
                      if (isEmbedded !== mode.value) {
                        handleEmbeddedToggle(null as any);
                      }
                    }}
                    sx={{
                      flex: { xs: '1 0 100%', sm: '1 0 45%' },
                      p: 2,
                      border: `2px solid ${isEmbedded === mode.value ? '#1976d2' : '#e0e0e0'}`,
                      borderRadius: 2,
                      cursor: 'pointer',
                      bgcolor: isEmbedded === mode.value ? '#e3f2fd' : '#fff',
                      transition: 'all 0.2s',
                      '&:hover': { bgcolor: '#f5f5f5' }
                    }}
                  >
                    <Box sx={{ display: 'flex', alignItems: 'center' }}>
                      <mode.icon sx={{ mr: 2, color: 'primary.main' }} />
                      <Box>
                        <Typography variant="subtitle1" fontWeight="bold">
                          {mode.label}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          {mode.description}
                        </Typography>
                      </Box>
                    </Box>
                  </Paper>
                ))}
              </Box>
              
              {isEmbedded && (
                <Alert severity="info" sx={{ mt: 2 }}>
                  <AlertTitle>Lưu ý về xác thực chữ ký nhúng</AlertTitle>
                  Chế độ xác thực chữ ký nhúng chỉ hỗ trợ tải lên file PDF có chữ ký số nhúng sẵn. Không hỗ trợ nhập văn bản thủ công.
                </Alert>
              )}
            </Box>

            {!isEmbedded && (
              <Box sx={{ display: 'flex', flexDirection: { xs: 'column', md: 'row' }, gap: 3 }}>
                <Box sx={{ flex: 1 }}>
                  <Paper sx={{ p: 3, borderRadius: 2, bgcolor: '#fafafa', display: 'flex', flexDirection: 'column' }}>
                    <Typography variant="subtitle1" fontWeight="bold" sx={{ mb: 2, display: 'flex', alignItems: 'center' }}>
                      <CreateIcon sx={{ mr: 1, color: 'primary.main' }} />
                      Chữ ký số
                    </Typography>
                    
                    <FormControl sx={{ mb: 2 }}>
                      <InputLabel>Cách nhập chữ ký</InputLabel>
                      <Select
                        value={signatureSource}
                        onChange={(e) => setSignatureSource(e.target.value as 'paste' | 'file')}
                        label="Cách nhập chữ ký"
                        size="small"
                      >
                        <MenuItem value="paste">Nhập trực tiếp</MenuItem>
                        <MenuItem value="file">Tải từ file</MenuItem>
                      </Select>
                    </FormControl>
                    
                    {signatureSource === 'paste' ? (
                      <TextField
                        label="Nhập chữ ký"
                        multiline
                        rows={4}
                        value={signature}
                        onChange={handleSignatureChange}
                        onPaste={handleSignaturePaste}
                        fullWidth
                        error={!!signatureError}
                        helperText={signatureError}
                        InputProps={{ sx: { fontFamily: 'monospace' } }}
                      />
                    ) : (
                      <Box sx={{ display: 'flex', flexDirection: 'column', flex: 1 }}>
                        <input
                          type="file"
                          ref={signatureFileInputRef}
                          style={{ display: 'none' }}
                          accept=".sig,.txt,.json"
                          onChange={handleSignatureFileSelect}
                        />
                        <Button
                          variant="outlined"
                          startIcon={<UploadFileIcon />}
                          onClick={() => signatureFileInputRef.current?.click()}
                          size="small"
                          sx={{ mb: 1 }}
                        >
                          Chọn file chữ ký
                        </Button>
                        {signatureFileName && (
                          <>
                            <Chip
                              label={signatureFileName}
                              onDelete={() => {
                                setSignatureFileName('');
                                setSignature('');
                              }}
                              size="small"
                              sx={{ mb: 1 }}
                            />
                            {signature && (
                              <Paper variant="outlined" sx={{ p: 1, bgcolor: '#f0f0f0', borderRadius: 1, flex: 1, minHeight: 0 }}>
                                <Typography variant="caption" color="textSecondary" sx={{ display: 'block', mb: 0.5 }}>
                                  Nội dung chữ ký:
                                </Typography>
                                <Box 
                                  sx={{ 
                                    p: 1, 
                                    bgcolor: '#fff', 
                                    borderRadius: 1, 
                                    height: '100px', 
                                    overflow: 'auto',
                                    fontFamily: 'monospace',
                                    fontSize: '0.75rem',
                                    whiteSpace: 'pre-wrap',
                                    wordBreak: 'break-all'
                                  }}
                                >
                                  {getSignaturePreview(signature)}
                                </Box>
                              </Paper>
                            )}
                          </>
                        )}
                      </Box>
                    )}
                  </Paper>
                </Box>

                <Box sx={{ flex: 1 }}>
                  <Paper sx={{ p: 3, borderRadius: 2, bgcolor: '#fafafa', display: 'flex', flexDirection: 'column' }}>
                    <Typography variant="subtitle1" fontWeight="bold" sx={{ mb: 2, display: 'flex', alignItems: 'center' }}>
                      <KeyIcon sx={{ mr: 1, color: 'primary.main' }} />
                      Khóa công khai
                    </Typography>
                    
                    <FormControl sx={{ mb: 2 }}>
                      <InputLabel>Cách nhập khóa công khai</InputLabel>
                      <Select
                        value={publicKeySource}
                        onChange={(e) => setPublicKeySource(e.target.value as 'select' | 'file')}
                        label="Cách nhập khóa công khai"
                        size="small"
                      >
                        <MenuItem value="select">Chọn từ danh sách</MenuItem>
                        <MenuItem value="file">Tải từ file</MenuItem>
                      </Select>
                    </FormControl>
                    
                    {publicKeySource === 'select' ? (
                      <FormControl>
                        <InputLabel>Chọn chữ ký</InputLabel>
                        <Select
                          value={selectedSignatureId}
                          onChange={handleSignatureChange_Select as any}
                          label="Chọn chữ ký"
                          size="small"
                        >
                          {signatures.map(sig => (
                            <MenuItem key={sig.id} value={sig.id}>
                              {sig.signatureName || sig.id}
                            </MenuItem>
                          ))}
                        </Select>
                      </FormControl>
                    ) : (
                      <Box sx={{ display: 'flex', flexDirection: 'column', flex: 1 }}>
                        <input
                          type="file"
                          ref={publicKeyFileInputRef}
                          style={{ display: 'none' }}
                          accept=".pem,.txt,.json"
                          onChange={handlePublicKeyFileSelect}
                        />
                        <Button
                          variant="outlined"
                          startIcon={<UploadFileIcon />}
                          onClick={() => publicKeyFileInputRef.current?.click()}
                          size="small"
                          sx={{ mb: 1 }}
                        >
                          Chọn file khóa công khai
                        </Button>
                        {publicKeyFileName && (
                          <>
                            <Chip
                              label={publicKeyFileName}
                              onDelete={() => {
                                setPublicKeyFileName('');
                                setPublicKey('');
                              }}
                              size="small"
                              sx={{ mb: 1 }}
                            />
                            {publicKey && (
                              <Paper variant="outlined" sx={{ p: 1, bgcolor: '#f0f0f0', borderRadius: 1, flex: 1, minHeight: 0 }}>
                                <Typography variant="caption" color="textSecondary" sx={{ display: 'block', mb: 0.5 }}>
                                  Nội dung khóa công khai:
                                </Typography>
                                <Box 
                                  sx={{ 
                                    p: 1, 
                                    bgcolor: '#fff', 
                                    borderRadius: 1, 
                                    height: '100px', 
                                    overflow: 'auto',
                                    fontFamily: 'monospace',
                                    fontSize: '0.75rem',
                                    whiteSpace: 'pre-wrap',
                                    wordBreak: 'break-all'
                                  }}
                                >
                                  {getPublicKeyPreview(publicKey)}
                                </Box>
                              </Paper>
                            )}
                          </>
                        )}
                      </Box>
                    )}
                  </Paper>
                </Box>
              </Box>
            )}

            {!isEmbedded && (
              <Paper sx={{ p: 3, mt: 3, borderRadius: 2, bgcolor: '#fafafa' }}>
                <Typography variant="subtitle1" fontWeight="bold" sx={{ mb: 2, display: 'flex', alignItems: 'center' }}>
                  <SettingsIcon sx={{ mr: 1, color: 'primary.main' }} />
                  Thuật toán băm
                </Typography>
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  {['MD5', 'SHA1', 'SHA256', 'SHA512'].map((algo) => (
                    <Chip
                      key={algo}
                      label={algo === 'SHA1' ? 'SHA-1' : 
                            algo === 'SHA256' ? 'SHA-256' : 
                            algo === 'SHA512' ? 'SHA-512' : algo}
                      clickable
                      color={hashAlgorithm === algo ? 'primary' : 'default'}
                      onClick={() => setHashAlgorithm(algo)}
                    />
                  ))}
                </Box>
              </Paper>
            )}
          </Paper>

          {/* Step 2: Document Selection */}
          <Paper 
            elevation={2} 
            sx={{ 
              p: { xs: 2, md: 3 }, 
              mb: 3, 
              borderRadius: 2,
              bgcolor: '#ffffff'
            }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
              <DescriptionIcon sx={{ color: 'primary.main', mr: 2, fontSize: 24 }} />
              <Typography variant="h6" fontWeight="bold">
                Bước 2: Chọn tài liệu
              </Typography>
            </Box>

            <Tabs 
              value={tabValue} 
              onChange={handleTabChange} 
              centered
              sx={{ mb: 3 }}
            >
              <Tab icon={<FileUploadIcon />} label="Tải file" />
              {!isEmbedded && (
                <Tab icon={<TextFieldsIcon />} label="Nhập văn bản" />
              )}
            </Tabs>

            <TabPanel value={tabValue} index={0}>
              <Box sx={{ maxWidth: 600, mx: 'auto' }}>
                <input
                  type="file"
                  ref={fileInputRef}
                  style={{ display: 'none' }}
                  onChange={handleFileSelect}
                />
                <Button
                  variant="contained"
                  startIcon={<FileUploadIcon />}
                  onClick={() => fileInputRef.current?.click()}
                  fullWidth
                  sx={{ py: 1.5 }}
                >
                  Chọn tài liệu
                </Button>

                {fileName && (
                  <Paper sx={{ p: 2, mt: 2, display: 'flex', alignItems: 'center', bgcolor: '#f5f5f5', borderRadius: 2 }}>
                    <DescriptionIcon sx={{ mr: 2, color: 'success.main' }} />
                    <Box>
                      <Typography variant="body1" fontWeight="bold">
                        {fileName}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        Kích thước: {fileSize}
                      </Typography>
                    </Box>
                  </Paper>
                )}

                {filePreview && (
                  <Paper sx={{ mt: 3, p: 2, bgcolor: '#fafafa', borderRadius: 2 }}>
                    <Typography variant="subtitle1" fontWeight="bold" sx={{ mb: 2 }}>
                      Xem trước
                    </Typography>
                    {filePreviewType === 'text' && (
                      <Box sx={{ maxHeight: 200, overflow: 'auto', p: 2, bgcolor: '#fff', borderRadius: 1 }}>
                        <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
                          {filePreview}
                        </Typography>
                      </Box>
                    )}
                    {filePreviewType === 'pdf' && (
                      <Box sx={{ height: 400, border: '1px solid #e0e0e0', borderRadius: 1, overflow: 'hidden' }}>
                        <iframe src={filePreview} style={{ width: '100%', height: '100%', border: 'none' }} />
                      </Box>
                    )}
                    {filePreviewType === 'image' && (
                      <Box sx={{ textAlign: 'center' }}>
                        <img src={filePreview} alt="Preview" style={{ maxWidth: '100%', maxHeight: 200 }} />
                      </Box>
                    )}
                    {filePreviewType === 'unsupported' && (
                      <Alert severity="info">
                        {filePreview}
                      </Alert>
                    )}
                  </Paper>
                )}
              </Box>
            </TabPanel>

            <TabPanel value={tabValue} index={1}>
              <Box sx={{ maxWidth: 600, mx: 'auto' }}>
                <TextField
                  label="Nội dung văn bản"
                  multiline
                  rows={6}
                  fullWidth
                  value={textContent}
                  onChange={(e) => setTextContent(e.target.value)}
                  placeholder="Nhập nội dung văn bản cần xác thực..."
                />
              </Box>
            </TabPanel>
          </Paper>

          {/* Step 3: Verify */}
          <Paper 
            elevation={2} 
            sx={{ 
              p: { xs: 2, md: 3 }, 
              mb: 3, 
              borderRadius: 2,
              bgcolor: '#ffffff'
            }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
              <VerifiedUserIcon sx={{ color: 'primary.main', mr: 2, fontSize: 24 }} />
              <Typography variant="h6" fontWeight="bold">
                Bước 3: Xác thực
              </Typography>
            </Box>

            <Box sx={{ maxWidth: 600, mx: 'auto', textAlign: 'center' }}>
              <Button
                variant="contained"
                size="large"
                onClick={handleVerify}
                disabled={isVerifying || 
                        (isEmbedded ? 
                          ((tabValue === 0 && !fileToVerify) || (tabValue === 1 && !textContent.trim())) : 
                          (!signature || !!signatureError || !publicKey || 
                          (tabValue === 0 && !fileToVerify) || (tabValue === 1 && !textContent.trim())))}
                startIcon={isVerifying ? <CircularProgress size={20} /> : <VerifiedUserIcon />}
                fullWidth
                sx={{ py: 1.5, mb: 2 }}
              >
                {isVerifying ? 'Đang xác thực...' : 'Xác thực tài liệu'}
              </Button>

              <Typography variant="body2" color="text.secondary">
                Nhấn nút để xác thực tài liệu với các thông tin đã chọn
              </Typography>
            </Box>
          </Paper>

          {/* Verification Result */}
          {verificationResult && (
        <Paper 
          sx={{ 
            p: { xs: 2, md: 3 }, 
            borderRadius: 2,
            border: `2px solid ${verificationResult.isValid ? '#4caf50' : verificationResult.tamperedDetected ? '#f44336' : '#ff9800'}`}
          }
        >
          <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
            <Typography variant="h5" fontWeight="bold">
              Kết quả xác thực
            </Typography>
            <Box sx={{ ml: 'auto' }}>
              {verificationResult.isValid ? (
                <CheckCircleIcon color="success" fontSize="large" />
              ) : (
                <CancelIcon color="error" fontSize="large" />
              )}
            </Box>
          </Box>

          <Alert 
            severity={verificationResult.isValid ? "success" : verificationResult.tamperedDetected ? "error" : "warning"}
            sx={{ mb: 3 }}
          >
            <AlertTitle>
              {verificationResult.isValid ? 'Hợp lệ' : 
               verificationResult.tamperedDetected ? 'Tài liệu đã bị sửa đổi' : 'Không hợp lệ'}
            </AlertTitle>
            {verificationResult.tamperedDetected ? 
              (verificationResult.message?.toLowerCase().includes('chữ ký') || 
               verificationResult.message?.toLowerCase().includes('signature') ? 
                'Chữ ký đã bị thay đổi hoặc không khớp với văn bản!' : 
                'Nội dung tài liệu đã bị thay đổi sau khi ký.') : 
              verificationResult.message}
          </Alert>

          {/* Display FullName and Email */}
          {(verificationResult.fullName || verificationResult.email) && (
            <Paper sx={{ p: 2, mb: 3, bgcolor: '#f5f5f5', borderRadius: 2 }}>
              <Typography variant="subtitle1" fontWeight="bold" sx={{ mb: 2 }}>
                Thông tin người ký
              </Typography>
              {verificationResult.fullName && (
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                  <Typography variant="body2" color="text.secondary" sx={{ mr: 2, minWidth: 100 }}>
                    Họ tên:
                  </Typography>
                  <Typography variant="body1">{verificationResult.fullName}</Typography>
                </Box>
              )}
              {verificationResult.email && (
                <Box sx={{ display: 'flex', alignItems: 'center' }}>
                  <Typography variant="body2" color="text.secondary" sx={{ mr: 2, minWidth: 100 }}>
                    Email:
                  </Typography>
                  <Typography variant="body1">{verificationResult.email}</Typography>
                </Box>
              )}
            </Paper>
          )}

          {(verificationResult.isValid || verificationResult.tamperedDetected) && verificationResult.metadata && (
            <Card sx={{ bgcolor: '#fafafa', borderRadius: 2 }}>
              <CardContent>
                <Typography variant="h6" gutterBottom>
                  Thông tin chi tiết
                </Typography>
                <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
                    <Paper sx={{ p: 2, flex: 1, minWidth: 200 }}>
                      <Typography variant="body2" color="text.secondary">Tên file</Typography>
                      <Typography variant="body1">{verificationResult.metadata.fileName || 'N/A'}</Typography>
                    </Paper>
                    <Paper sx={{ p: 2, flex: 1, minWidth: 200 }}>
                      <Typography variant="body2" color="text.secondary">Kích thước</Typography>
                      <Typography variant="body1">
                        {verificationResult.metadata.fileSize ? `${(verificationResult.metadata.fileSize / 1024).toFixed(2)} KB` : 'N/A'}
                      </Typography>
                    </Paper>
                  </Box>
                  <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
                    <Paper sx={{ p: 2, flex: 1, minWidth: 200 }}>
                      <Typography variant="body2" color="text.secondary">Thuật toán băm</Typography>
                      <Typography variant="body1">{verificationResult.metadata.hashAlgorithm || 'N/A'}</Typography>
                    </Paper>
                    <Paper sx={{ p: 2, flex: 1, minWidth: 200 }}>
                      <Typography variant="body2" color="text.secondary">Thời gian ký</Typography>
                      <Typography variant="body1">
                        {verificationResult.metadata.timestamp ? new Date(verificationResult.metadata.timestamp).toLocaleString() : 'N/A'}
                      </Typography>
                    </Paper>
                  </Box>
                  {verificationResult.tamperedDetected && verificationResult.metadata.originalHash && (
                    <>
                      <Paper sx={{ p: 2, bgcolor: '#ffebee' }}>
                        <Typography variant="body2" color="text.secondary">Giá trị băm ban đầu</Typography>
                        <Typography variant="body2" sx={{ fontFamily: 'monospace', wordBreak: 'break-all' }}>
                          {verificationResult.metadata.originalHash}
                        </Typography>
                      </Paper>
                      <Paper sx={{ p: 2, bgcolor: '#e8f5e9' }}>
                        <Typography variant="body2" color="text.secondary">Giá trị băm hiện tại</Typography>
                        <Typography variant="body2" sx={{ fontFamily: 'monospace', wordBreak: 'break-all' }}>
                          {verificationResult.metadata.currentHash}
                        </Typography>
                      </Paper>
                    </>
                  )}
                </Box>
              </CardContent>
            </Card>
          )}
        </Paper>
      )}
        </Paper>
      </Container>
    </Box>
  );
};


export default Verify_file;