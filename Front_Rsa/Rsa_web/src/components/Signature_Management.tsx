import React, { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import { useNavigate } from 'react-router-dom';
import {
  Box, Button, Paper, Table, TableHead, TableRow, TableCell,
  TableBody, Typography, Chip, IconButton, Stack, Dialog,
  DialogTitle, DialogContent, DialogActions, MenuItem, Alert, Snackbar, AppBar, Toolbar, Container,
  TextField, CircularProgress
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import UploadIcon from '@mui/icons-material/UploadFile';
import VisibilityIcon from '@mui/icons-material/Visibility';
import DownloadIcon from '@mui/icons-material/Download';
import DeleteIcon from '@mui/icons-material/Delete';
import MenuIcon from '@mui/icons-material/Menu';
import LinkIcon from '@mui/icons-material/Link';

const API_URL = '/api';

// Setup axios interceptors for debugging
axios.interceptors.request.use(request => {
  console.log('API Request:', { 
    url: request.url, 
    method: request.method, 
    data: request.data 
  });
  return request;
});

axios.interceptors.response.use(
  response => {
    console.log('API Response:', { 
      url: response.config.url, 
      status: response.status, 
      data: response.data 
    });
    return response;
  },
  error => {
    console.error('API Error:', { 
      url: error.config?.url, 
      status: error.response?.status, 
      data: error.response?.data,
      message: error.message 
    });
    return Promise.reject(error);
  }
);

interface Signature {
  id: string;
  publicKey: string;
  createdAt: string;
  signatureName: string;
  signatureType: string;
  email?: string;
  fullName?: string;
  isActive: boolean;
  privateKey?: string; // Add privateKey for editing
  p?: string;
  q?: string;
  e?: string;
  d?: string;
}

const SignatureManagement: React.FC = () => {
  const navigate = useNavigate();
  const [signatures, setSignatures] = useState<Signature[]>([]);
  
  const [openGen, setOpenGen] = useState(false);
  const [keySaved, setKeySaved] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  
  // State for loading indicators
  const [isGeneratingKey, setIsGeneratingKey] = useState(false);
  
  // State for edit mode
  const [isEditMode, setIsEditMode] = useState(false);
  const [editingSignatureId, setEditingSignatureId] = useState<string | null>(null);

  // New state for the key generation form
  const [keyParams, setKeyParams] = useState({
    p: '',
    q: '',
    e: '',
    d: '',
    keyType: 'Manual',
    publicKey: '',
    privateKey: '',
    signatureName: '',
    n: ''  // Add n parameter to represent the modulus
  });

  // State for file signing
  const [openSignDialog, setOpenSignDialog] = useState(false);
  const [selectedSignId, setSelectedSignId] = useState<string>('');
  const [fileToSign, setFileToSign] = useState<File | null>(null);
  const [fileName, setFileName] = useState<string>('');
  const [fileContent, setFileContent] = useState<string>('');
  const [hashAlgorithm, setHashAlgorithm] = useState<string>('SHA256');
  const [signature, setSignature] = useState<string>('');
  const [isSigningFile, setIsSigningFile] = useState(false);

  // State for import confirmation
  const [openImportDialog, setOpenImportDialog] = useState(false);
  const [importKeyData, setImportKeyData] = useState<any>(null);
  const [importFileContent, setImportFileContent] = useState('');
  const [isImporting, setIsImporting] = useState(false);
  const [importProgress, setImportProgress] = useState<{
    step: 'validating' | 'saving' | 'complete';
    message: string;
  }>({ step: 'validating', message: 'Đang xác thực khóa...' });

  // State for notifications
  const [notification, setNotification] = useState({
    open: false,
    message: '',
    severity: 'info' as 'success' | 'info' | 'warning' | 'error'
  });

  const fetchList = async () => {
    try {
      const userId = localStorage.getItem('userId')!;
      console.log('Fetching signatures for userId:', userId);
      const res = await axios.get<{ signatures: Signature[] }>(
        `${API_URL}/sign/list/${userId}`
      );
      setSignatures(res.data.signatures);
    } catch (err) {
      console.error(err);
      showNotification('Failed to fetch signatures', 'error');
    }
  };

  // Function to simulate login (for testing only)
  const simulateLogin = () => {
    // This is for development/testing only - in production, actual login should be used
    const testUserId = "1234567890"; // Example userId
    localStorage.setItem('userId', testUserId);
    console.log('Simulated login with userId:', testUserId);
    showNotification('Simulated login successful', 'success');
    fetchList();
  };

  useEffect(() => {
    // Check if userId exists in localStorage
    const userId = localStorage.getItem('userId');
    console.log('UserId in localStorage:', userId);
    
    if (!userId) {
      showNotification('User not logged in. Please log in first.', 'error');
      // For development purposes only - auto login simulation
      simulateLogin();
      return;
    }
    
    fetchList();
  }, []);

  // Navigate to home page
  const goToHome = () => {
    navigate('/homepage');
  };

  const handleGenerateRsaKey = async () => {
    try {
      const userId = localStorage.getItem('userId')!;
      const keySize = parseInt(keyParams.keyType.replace('Rsa', ''));
      
      const signatureName = keyParams.signatureName || `RSA-${keySize}-${new Date().toISOString().slice(0, 10)}`;
      
      console.log('Generating RSA key with size:', keySize);
      
      // Show notification that key generation is in progress
      showNotification(`Đang tạo khóa RSA ${keySize}-bit, vui lòng đợi...`, 'info');
      
      // Add timeout for larger keys
      const timeoutMs = keySize >= 4096 ? 60000 : 30000; // 60s for 4096-bit, 30s for others
      
      const response = await axios.post(`${API_URL}/sign/generate-keys`, { 
        userId, 
        keySize,
        signatureName: signatureName,
        signatureType: `Rsa${keySize}`
      }, {
        timeout: timeoutMs
      });
      
      console.log('RSA key generation response:', response.data);
      
      // Extract public and private keys from the response
      if (response.data) {
        // Try different response formats
        const publicKey = response.data.publicKey || 
                         (response.data.keyPair && response.data.keyPair.publicKey) || 
                         '';
        const privateKey = response.data.privateKey || 
                          (response.data.keyPair && response.data.keyPair.privateKey) || 
                          '';
        
        if (publicKey && privateKey) {
          // Store the keys
          setKeyParams(prev => ({
            ...prev,
            publicKey,
            privateKey
          }));
          
          showNotification(`RSA ${keySize} key generated successfully`, 'success');
          setKeySaved(false); // Keys are generated but not yet saved
          return true;
        } else {
          showNotification('Failed to extract key pair from response', 'error');
          return false;
        }
      } else {
        showNotification('Invalid response from server', 'error');
        return false;
      }
    } catch (error: any) {
      console.error('RSA key generation error:', error);
      
      // More detailed error message
      let errorMessage = 'Failed to generate RSA key';
      
      if (error.code === 'ECONNABORTED') {
        errorMessage = 'Key generation timed out. Server may be busy or key size too large.';
      } else if (error.response) {
        // Server responded with an error
        errorMessage = `Server error: ${error.response.data?.error || error.response.data?.message || error.response.statusText}`;
      } else if (error.request) {
        // Request was made but no response received
        errorMessage = 'No response from server. Please check your connection.';
      } else if (error.message) {
        errorMessage = error.message;
      }
      
      showNotification(errorMessage, 'error');
      return false;
    }
  };

  // Use very simple hardcoded key for auto
  const generateSimpleKey = () => {
    try {
      // Hardcoded simple values for testing
      const p = "11";
      const q = "13";
      const n = "143";
      const e = "7";
      const d = "103";  // Modular inverse of 7 mod 120
      
      console.log("Generated simple key with values:", { p, q, e, d, n });
      
      // Format keys as (e,n) and (d,n)
      const publicKeyObj = { e, n };
      const privateKeyObj = { d, n };
      
      const publicKeyStr = JSON.stringify(publicKeyObj);
      const privateKeyStr = JSON.stringify(privateKeyObj);
      
      console.log("Generated key strings:", { publicKeyStr, privateKeyStr });
      
      // Set all parameters
      setKeyParams(prev => ({
        ...prev,
        p,
        q,
        e,
        d,
        n,
        publicKey: publicKeyStr,
        privateKey: privateKeyStr
      }));
      
      showNotification('Khóa đã được tạo thành công', 'success');
      return true;
    } catch (error) {
      console.error('Error generating simple key:', error);
      showNotification('Lỗi tạo khóa đơn giản', 'error');
      return false;
    }
  };

  const generateEDFromPQ = async () => {
    try {
      if (!keyParams.p || !keyParams.q) {
        showNotification('Both P and Q are required to generate E,D values', 'error');
        return false;
      }
      
      console.log('Generating E,D from P,Q:', {
        p: keyParams.p,
        q: keyParams.q
      });
      
      const response = await axios.post(`${API_URL}/manualsign/generate-ed`, {
        p: keyParams.p,
        q: keyParams.q
      });
      
      console.log('Generate E,D response:', response.data);
      
      if (!response.data || !response.data.e || !response.data.d) {
        showNotification('Failed to generate E,D values', 'error');
        return false;
      }
      
      const e = response.data.e;
      const d = response.data.d;
      
      // Calculate n from p and q - use simple multiplication instead of BigInt
      const p = parseInt(keyParams.p);
      const q = parseInt(keyParams.q);
      const n = (p * q).toString();
      
      // Format keys as (e,n) and (d,n)
      const publicKeyObj = { e, n };
      const privateKeyObj = { d, n };
      
      const publicKeyStr = JSON.stringify(publicKeyObj);
      const privateKeyStr = JSON.stringify(privateKeyObj);
      
      console.log("Generated key strings from P,Q:", { publicKeyStr, privateKeyStr });
      
      setKeyParams(prev => ({
        ...prev,
        e,
        d,
        n,
        publicKey: publicKeyStr,
        privateKey: privateKeyStr
      }));
      
      showNotification('Khóa được tạo thành công', 'success');
      return true;
    } catch (error) {
      console.error('Error generating E,D:', error);
      showNotification('Failed to generate E,D values', 'error');
      return false;
    }
  };

  const handleSaveKey = async () => {
    if (!keyParams.signatureName) {
      showNotification('Vui lòng nhập tên khóa', 'error');
      return false;
    }
    
    if (!keyParams.publicKey || !keyParams.privateKey) {
      showNotification('Khóa chưa được tạo', 'error');
      return false;
    }
    
    try {
      const userId = localStorage.getItem('userId')!;
      
      // For RSA keys using the original format
      let finalPublicKey = keyParams.publicKey;
      let finalPrivateKey = keyParams.privateKey;
      let finalSignatureType = keyParams.keyType === 'Manual' ? 'Manual' : 
                               keyParams.keyType === 'Auto' ? 'Auto' : keyParams.keyType;
                               
      // Log for debugging
      console.log('Saving keys with params:', {
        publicKey: finalPublicKey,
        privateKey: finalPrivateKey,
        signatureName: keyParams.signatureName,
        signatureType: finalSignatureType,
        userId: userId,
        p: keyParams.p,
        q: keyParams.q,
        e: keyParams.e,
        d: keyParams.d,
        isEditMode: isEditMode,
        editingSignatureId: editingSignatureId
      });
      
      let response;
      
      if (isEditMode && editingSignatureId) {
        // Update existing key
        response = await axios.put(`${API_URL}/manualsign/update-key-pair`, {
          signId: editingSignatureId,
          publicKey: finalPublicKey,
          privateKey: finalPrivateKey,
          signatureName: keyParams.signatureName,
          signatureType: finalSignatureType,
          userId: userId,
          p: keyParams.p || "0",
          q: keyParams.q || "0",
          e: keyParams.e || "0",
          d: keyParams.d || "0"
        });
        
        console.log('Update key response:', response.data);
        showNotification('Khóa đã được cập nhật thành công', 'success');
      } else {
        // Create new key
        response = await axios.post(`${API_URL}/manualsign/save-key-pair`, {
          publicKey: finalPublicKey,
          privateKey: finalPrivateKey,
          signatureName: keyParams.signatureName,
          signatureType: finalSignatureType,
          userId: userId,
          p: keyParams.p || "0",
          q: keyParams.q || "0",
          e: keyParams.e || "0",
          d: keyParams.d || "0"
        });
        
        console.log('Save key response:', response.data);
        showNotification('Khóa đã được lưu thành công', 'success');
      }
      
      setKeySaved(true);
      fetchList();
      return true;
    } catch (error) {
      console.error('Key saving error:', error);
      showNotification('Lưu khóa không thành công', 'error');
      return false;
    }
  };

  const generateAutoParams = async () => {
    try {
      // Use simple hardcoded key for reliability
      return generateSimpleKey();
    } catch (error) {
      console.error('Auto parameters error:', error);
      showNotification('Lỗi tạo tham số khóa tự động', 'error');
      return false;
    }
  };

  const handleCreateKey = async () => {
    if (!keyParams.signatureName) {
      showNotification('Vui lòng nhập tên khóa', 'error');
      return;
    }
    
    let success = false;
    setIsGeneratingKey(true);
    
    try {
      if (keyParams.keyType === 'Auto') {
        // Auto mode: Generate small parameters
        success = await generateAutoParams();
      } 
      else if (keyParams.keyType === 'Manual') {
        // Manual mode: Generate E,D from P,Q
        if (!keyParams.p || !keyParams.q) {
          showNotification('Vui lòng nhập giá trị P và Q', 'error');
          return;
        }
        
        success = await generateEDFromPQ();
      }
      else if (keyParams.keyType.startsWith('Rsa')) {
        // RSA mode: Generate standard RSA keys
        success = await handleGenerateRsaKey();
      }
      
      if (success) {
        console.log('Key generation successful. Ready for saving.');
      }
    } catch (error) {
      console.error('Error in key generation:', error);
      showNotification('Lỗi không xác định khi tạo khóa', 'error');
    } finally {
      setIsGeneratingKey(false);
    }
  };

  const handleConfirm = async () => {
    console.log('Handling confirmation, keySaved:', keySaved);
    
    if (keySaved) {
      // If already saved, just close the dialog
      setOpenGen(false);
      resetForm();
      return;
    }
    
    // Check if we have keys to save
    if (!keyParams.publicKey || !keyParams.privateKey) {
      showNotification('Không có khóa để lưu. Vui lòng tạo khóa trước.', 'error');
      return;
    }
    
    // Check if we have a name
    if (!keyParams.signatureName) {
      showNotification('Vui lòng nhập tên khóa', 'error');
      return;
    }
    
    // Save the key
    console.log('Attempting to save key...');
    const success = await handleSaveKey();
    if (success) {
      showNotification(isEditMode ? 'Khóa đã được cập nhật thành công' : 'Khóa đã được lưu thành công', 'success');
      setOpenGen(false);
      resetForm();
    }
  };

  const handleDelete = async (id: string) => {
    try {
      const userId = localStorage.getItem('userId')!;
      await axios.delete(`${API_URL}/sign/delete/${userId}/${id}`);
      fetchList();
      showNotification('Signature deleted successfully', 'success');
    } catch (error) {
      console.error(error);
      showNotification('Failed to delete signature', 'error');
    }
  };

  const handleImport = () => fileInputRef.current?.click();
  const onFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    try {
      if (!event.target.files || event.target.files.length === 0) {
        return;
      }
      
      const file = event.target.files[0];
      
      // Check if file is JSON
      if (!file.name.toLowerCase().endsWith('.json')) {
        showNotification('Chỉ chấp nhận file định dạng JSON', 'error');
        return;
      }
      
      // Read file content
      const reader = new FileReader();
      
      reader.onload = async (e) => {
        try {
          const fileContent = e.target?.result as string;
          
          // Parse JSON to validate format
          const keyData = JSON.parse(fileContent);
          
          // Check if the file has the required keys
          if (!keyData.publicKey || !keyData.privateKey) {
            showNotification('File không chứa thông tin khóa hợp lệ', 'error');
            return;
          }
          
          // Get user ID
          const userId = localStorage.getItem('userId');
          if (!userId) {
            showNotification('Bạn chưa đăng nhập', 'error');
            return;
          }
          
          // Set import data and open confirmation dialog
          setImportKeyData(keyData);
          setImportFileContent(fileContent);
          setOpenImportDialog(true);
        } catch (error) {
          console.error('Error processing key file:', error);
          showNotification('Lỗi xử lý file khóa: Định dạng JSON không hợp lệ', 'error');
        }
        
        // Reset the file input
        if (event.target) {
          event.target.value = '';
        }
      };
      
      reader.onerror = () => {
        showNotification('Lỗi đọc file', 'error');
      };
      
      // Read the file as text
      reader.readAsText(file);
    } catch (error) {
      console.error('Error importing key file:', error);
      showNotification('Lỗi nhập khóa', 'error');
    }
  };

  // Handle key import confirmation
  const handleImportConfirm = async () => {
    try {
      if (!importKeyData || !importFileContent) {
        showNotification('Không có dữ liệu khóa để nhập', 'error');
        return;
      }
      
      const userId = localStorage.getItem('userId');
      if (!userId) {
        showNotification('Bạn chưa đăng nhập', 'error');
        return;
      }
      
      // Prepare signature name and type
      const signatureName = importKeyData.signatureName || `Imported-${new Date().toISOString().slice(0, 10)}`;
      const signatureType = importKeyData.signatureType || 'Imported';
      
      // Start import process
      setIsImporting(true);
      setImportProgress({ 
        step: 'validating', 
        message: 'Đang xác thực khóa...' 
      });
      
      // Close confirmation dialog and open progress dialog
      setOpenImportDialog(false);
      
      // Call the API to import keys
      try {
        const response = await axios.post(`${API_URL}/manualsign/import-keys`, {
          keyFileContent: importFileContent,
          userId: userId,
          signatureName: signatureName,
          signatureType: signatureType
        });
        
        console.log('Import keys response:', response.data);
        
        if (response.data && response.data.publicKey && response.data.privateKey) {
          setImportProgress({ 
            step: 'saving', 
            message: 'Khóa hợp lệ, đang lưu...' 
          });
          
          // Save the imported keys
          const saveResponse = await axios.post(`${API_URL}/manualsign/save-key-pair`, {
            publicKey: response.data.publicKey,
            privateKey: response.data.privateKey,
            signatureName: signatureName,
            signatureType: signatureType,
            userId: userId,
            p: importKeyData.p || "0",
            q: importKeyData.q || "0",
            e: importKeyData.e || "0",
            d: importKeyData.d || "0"
          });
          
          console.log('Save imported key response:', saveResponse.data);
          
          if (saveResponse.data && saveResponse.data.signId) {
            setImportProgress({ 
              step: 'complete', 
              message: 'Khóa đã được nhập thành công!' 
            });
            
            // Wait a moment to show completion message
            setTimeout(() => {
              setIsImporting(false);
              showNotification('Khóa đã được nhập thành công', 'success');
              fetchList(); // Refresh the list
            }, 1500);
          } else {
            setIsImporting(false);
            showNotification('Lưu khóa không thành công', 'error');
          }
        } else {
          setIsImporting(false);
          showNotification('Nhập khóa không thành công', 'error');
        }
      } catch (error: any) {
        console.error('Error importing key:', error);
        
        // Extract detailed error message from response if available
        let errorMessage = 'Lỗi nhập khóa';
        if (error.response && error.response.data) {
          if (error.response.data.error) {
            errorMessage = error.response.data.error;
          }
        }
        
        setIsImporting(false);
        showNotification(`Lỗi: ${errorMessage}`, 'error');
      }
    } catch (error) {
      console.error('Error in import confirmation:', error);
      setIsImporting(false);
      showNotification('Lỗi xử lý nhập khóa', 'error');
    }
  };

  const handleImportCancel = () => {
    setOpenImportDialog(false);
    setImportKeyData(null);
    setImportFileContent('');
  };

  const handleInputChange = (field: string) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setKeyParams({
      ...keyParams,
      [field]: e.target.value
    });
  };

  const handleKeyTypeChange = (e: any) => {
    setKeyParams({
      ...keyParams,
      keyType: e.target.value,
      // Reset fields when changing key type
      p: '',
      q: '',
      e: '',
      d: '',
      publicKey: '',
      privateKey: '',
      n: '',
      signatureName: '',
    });
    setKeySaved(false);
  };

  const showNotification = (message: string, severity: 'success' | 'info' | 'warning' | 'error') => {
    setNotification({
      open: true,
      message,
      severity
    });
  };

  const handleCloseNotification = () => {
    setNotification({
      ...notification,
      open: false
    });
  };

  const resetForm = () => {
    setKeyParams({
      p: '',
      q: '',
      e: '',
      d: '',
      keyType: 'Manual',
      publicKey: '',
      privateKey: '',
      signatureName: '',
      n: ''
    });
    setKeySaved(false);
    setIsEditMode(false);
    setEditingSignatureId(null);
  };

  const handleCancel = () => {
    resetForm();
    setOpenGen(false);
  };

  // Determine if p, q, e, d fields should be shown
  const showParamFields = keyParams.keyType === 'Manual' || keyParams.keyType === 'Auto';

  const handleEdit = async (id: string) => {
    try {
      // Find the signature in the current list
      const signatureToEdit = signatures.find(sig => sig.id === id);
      
      if (!signatureToEdit) {
        showNotification('Signature not found', 'error');
        return;
      }
      
      // For security reasons, we need to fetch the private key from the backend
      const userId = localStorage.getItem('userId')!;
      const response = await axios.get(`${API_URL}/manualsign/get-key-details/${userId}/${id}`);
      
      console.log('Edit signature response:', response.data);
      
      if (response.data && response.data.signature) {
        const fullSignature = response.data.signature;
        
        // Set the form in edit mode
        setIsEditMode(true);
        setEditingSignatureId(id);
        
        // Parse the keys if they're in JSON format
        let publicKeyObj: Record<string, string> = {};
        let privateKeyObj: Record<string, string> = {};
        let pValue = '';
        let qValue = '';
        let eValue = '';
        let dValue = '';
        let nValue = '';
        
        try {
          if (fullSignature.publicKey) {
            publicKeyObj = JSON.parse(fullSignature.publicKey);
            if (publicKeyObj.e && publicKeyObj.n) {
              eValue = publicKeyObj.e;
              nValue = publicKeyObj.n;
            }
          }
          
          if (fullSignature.privateKey) {
            privateKeyObj = JSON.parse(fullSignature.privateKey);
            if (privateKeyObj.d && privateKeyObj.n) {
              dValue = privateKeyObj.d;
            }
          }
          
          // Set p and q if available
          pValue = fullSignature.p || '';
          qValue = fullSignature.q || '';
        } catch (error) {
          console.error('Error parsing key data:', error);
        }
        
        // Set form values
        setKeyParams({
          p: pValue,
          q: qValue,
          e: eValue,
          d: dValue,
          n: nValue,
          keyType: fullSignature.signatureType || 'Manual',
          publicKey: fullSignature.publicKey || '',
          privateKey: fullSignature.privateKey || '',
          signatureName: fullSignature.signatureName || ''
        });
        
        // Open the dialog
        setOpenGen(true);
      } else {
        showNotification('Failed to fetch signature details', 'error');
      }
    } catch (error) {
      console.error('Error editing signature:', error);
      showNotification('Failed to edit signature', 'error');
    }
  };

  const handleExport = async (id: string) => {
    try {
      const userId = localStorage.getItem('userId')!;
      
      // Call the export endpoint
      const response = await axios.post(
        `${API_URL}/manualsign/export-keys`,
        {
          userId,
          signId: id,
          format: 'json'  // Request JSON format
        },
        {
          responseType: 'blob'  // Important for file download
        }
      );
      
      console.log('Export response received');
      
      // Create a download link
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement('a');
      
      // Find the signature to use its name in the filename
      const signature = signatures.find(sig => sig.id === id);
      const filename = signature ? 
        `${signature.signatureName.replace(/\s+/g, '_')}_keys.json` : 
        `signature_${id}_keys.json`;
      
      link.href = url;
      link.setAttribute('download', filename);
      document.body.appendChild(link);
      link.click();
      
      // Clean up
      window.URL.revokeObjectURL(url);
      document.body.removeChild(link);
      
      showNotification('Keys exported successfully', 'success');
    } catch (error) {
      console.error('Error exporting keys:', error);
      showNotification('Failed to export keys', 'error');
    }
  };

  // Handle file selection for signing
  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    if (event.target.files && event.target.files.length > 0) {
      const file = event.target.files[0];
      setFileToSign(file);
      setFileName(file.name);
      
      // Giới hạn kích thước file
      if (file.size > 10 * 1024 * 1024) { // 10MB
        showNotification('Kích thước file vượt quá giới hạn 10MB', 'error');
        return;
      }
      
      // Xử lý file dựa trên loại file
      if (file.type.includes('text') || 
          file.name.endsWith('.txt') || 
          file.name.endsWith('.md') || 
          file.name.endsWith('.json') || 
          file.name.endsWith('.csv')) {
        // Đối với file văn bản, đọc trực tiếp nội dung
        const reader = new FileReader();
        reader.onload = (e) => {
          try {
            const content = e.target?.result as string;
            // Hiển thị nội dung giới hạn
            const maxLength = 3000;
            setFileContent(content.substring(0, maxLength) + (content.length > maxLength ? '...' : ''));
            showNotification('Đã tải file văn bản thành công', 'success');
          } catch (error) {
            console.error('Error reading text file:', error);
            setFileContent('Không thể đọc nội dung file văn bản.');
            showNotification('Không thể đọc nội dung file văn bản', 'error');
          }
        };
        reader.onerror = () => {
          setFileContent('Lỗi: Không thể đọc file.');
          showNotification('Lỗi đọc file', 'error');
        };
        reader.readAsText(file);
      } else {
        // Đối với các định dạng khác được hỗ trợ
        setFileContent(`[File] - ${file.name} (${(file.size / 1024).toFixed(2)} KB)\nSẵn sàng để ký.`);
        showNotification('Đã tải file thành công', 'success');
      }
    }
  };

  // Handle signing a file
  const handleSignFile = async () => {
    if (!fileToSign) {
      showNotification('Vui lòng chọn file để ký', 'error');
      return;
    }
    
    if (!selectedSignId) {
      showNotification('Vui lòng chọn chữ ký', 'error');
      return;
    }
    
    setIsSigningFile(true);
    setSignature('');
    
    try {
      const userId = localStorage.getItem('userId');
      if (!userId) {
        showNotification('Bạn chưa đăng nhập', 'error');
        setIsSigningFile(false);
        return;
      }
      
      // Prepare form data for file upload
      const formData = new FormData();
      formData.append('file', fileToSign);
      formData.append('userId', userId);
      formData.append('signatureId', selectedSignId);
      formData.append('hashAlgorithm', hashAlgorithm);
      
      console.log('Signing file with parameters:', {
        userId,
        signatureId: selectedSignId,
        hashAlgorithm,
        fileName: fileToSign.name
      });
      
      // Call the API to sign the file
      const response = await axios.post(`${API_URL}/manualsign/sign-file`, formData, {
        headers: {
          'Content-Type': 'multipart/form-data'
        }
      });
      
      console.log('Sign file response:', response.data);
      
      if (response.data && response.data.signature) {
        setSignature(response.data.signature);
        showNotification('File đã được ký thành công', 'success');
        
        // Add public key export option
        const selectedSignature = signatures.find(s => s.id === selectedSignId);
        if (selectedSignature && selectedSignature.publicKey) {
          // We could add public key export functionality here if needed
        }
      } else {
        showNotification('Không nhận được chữ ký từ máy chủ', 'error');
      }
    } catch (error: any) {
      console.error('Error signing file:', error);
      let errorMessage = 'Lỗi khi ký file';
      
      // Extract more specific error message if available
      if (error.response && error.response.data) {
        if (error.response.data.error) {
          errorMessage += ': ' + error.response.data.error;
        } else if (typeof error.response.data === 'string') {
          errorMessage += ': ' + error.response.data;
        }
      } else if (error.message) {
        errorMessage += ': ' + error.message;
      }
      
      showNotification(errorMessage, 'error');
    } finally {
      setIsSigningFile(false);
    }
  };

  // Copy signature to clipboard
  const copyToClipboard = () => {
    if (signature) {
      navigator.clipboard.writeText(signature)
        .then(() => showNotification('Đã sao chép chữ ký vào clipboard', 'success'))
        .catch(() => showNotification('Không thể sao chép chữ ký', 'error'));
    }
  };

  // Open sign dialog
  const handleOpenSignDialog = (id: string) => {
    setSelectedSignId(id);
    setOpenSignDialog(true);
    setFileToSign(null);
    setFileName('');
    setFileContent('');
    setSignature('');
  };

  return (
    <>
      <AppBar position="static">
        <Toolbar>
          <LinkIcon sx={{ mr: 1 }} />
          <Typography 
            variant="h6" 
            sx={{ flexGrow: 1, cursor: 'pointer' }} 
            onClick={goToHome}
          >
            RSA DIGITAL SIGNATURE
          </Typography>
          <IconButton edge="end" color="inherit">
            <MenuIcon />
          </IconButton>
        </Toolbar>
      </AppBar>
      
      <Container>
        <Box p={4}>
          <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
            <Typography variant="h5">Quản lý Chữ ký</Typography>
            <Stack direction="row" spacing={2}>
              <Button
                variant="contained"
                startIcon={<AddIcon />}
                onClick={() => {
                  resetForm();
                  setOpenGen(true);
                }}
              >
                Thêm chữ ký
              </Button>
              <Button
                variant="outlined"
                startIcon={<UploadIcon />}
                onClick={handleImport}
              >
                Nhập
              </Button>
              <input
                ref={fileInputRef}
                type="file"
                accept=".json"
                hidden
                onChange={onFileChange}
              />
            </Stack>
          </Box>

          <Paper>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Tên</TableCell>
                  <TableCell>Loại</TableCell>
                  <TableCell>Trạng thái</TableCell>
                  <TableCell>Ngày tạo</TableCell>
                  <TableCell align="center">Thao tác</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {signatures.map(sig => (
                  <TableRow key={sig.id}>
                    <TableCell>{sig.signatureName || sig.id}</TableCell>
                    <TableCell>{sig.signatureType || 'RSA'}</TableCell>
                    <TableCell>
                      <Chip label={sig.isActive ? "Hoạt động" : "Vô hiệu"} 
                            color={sig.isActive ? "success" : "error"} 
                            size="small" />
                    </TableCell>
                    <TableCell>
                      {new Date(sig.createdAt).toLocaleDateString()}
                    </TableCell>
                    <TableCell align="center">
                      <Stack direction="row" spacing={1} justifyContent="center"> 
                        <IconButton 
                          size="small"
                          onClick={() => handleEdit(sig.id)}
                        >
                          <VisibilityIcon fontSize="small" />
                        </IconButton>
                        <IconButton 
                          size="small"
                          onClick={() => handleExport(sig.id)}
                        >
                          <DownloadIcon fontSize="small" />
                        </IconButton>
                       
                        <IconButton
                          size="small"
                          onClick={() => handleDelete(sig.id)}
                        >
                          <DeleteIcon fontSize="small" color="error" />
                        </IconButton>
                      </Stack>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Paper>

          {/* Generate Key Dialog */}
          <Dialog 
            open={openGen} 
            onClose={() => setOpenGen(false)} 
            maxWidth="sm" 
            fullWidth
          >
            <DialogTitle>{isEditMode ? 'CHỈNH SỬA KHÓA' : 'THÊM KHÓA'}</DialogTitle>
            <DialogContent>
              <Box sx={{ mt: 2 }}>
                <TextField
                  fullWidth
                  label="Tên khóa"
                  margin="dense"
                  value={keyParams.signatureName}
                  onChange={handleInputChange('signatureName')}
                  placeholder="Nhập tên khóa"
                />
                
                <TextField
                  select
                  fullWidth
                  label="Chọn loại khóa"
                  margin="dense"
                  value={keyParams.keyType}
                  onChange={handleKeyTypeChange}
                  disabled={isEditMode} // Disable type change in edit mode
                >
                  <MenuItem value="Manual">Thủ công</MenuItem>
                  <MenuItem value="Auto">Tự động</MenuItem>
                  <MenuItem value="Rsa2048">RSA 2048</MenuItem>
                  <MenuItem value="Rsa3072">RSA 3072</MenuItem>
                  <MenuItem value="Rsa4096">RSA 4096</MenuItem>
                </TextField>
                
                {showParamFields && (
                  <>
                    <TextField
                      fullWidth
                      label="Nhập P"
                      margin="dense"
                      value={keyParams.p}
                      onChange={handleInputChange('p')}
                      disabled={keyParams.keyType === 'Auto' || isEditMode}
                    />
                    
                    <TextField
                      fullWidth
                      label="Nhập Q"
                      margin="dense"
                      value={keyParams.q}
                      onChange={handleInputChange('q')}
                      disabled={keyParams.keyType === 'Auto' || isEditMode}
                    />
                  </>
                )}
                
                {/* Public and Private Key fields always visible */}
                <TextField
                  fullWidth
                  label="Public Key (e,n)"
                  margin="dense"
                  value={keyParams.publicKey}
                  InputProps={{
                    readOnly: true,
                  }}
                />
                
                <TextField
                  fullWidth
                  label="Private Key (d,n)"
                  margin="dense"
                  value={keyParams.privateKey}
                  InputProps={{
                    readOnly: true,
                  }}
                />
              </Box>
            </DialogContent>
            <DialogActions sx={{ justifyContent: 'center', pb: 2, gap: 2 }}>
              {!isEditMode && (
                <Button 
                  variant="contained" 
                  color="primary"
                  onClick={handleCreateKey}
                  disabled={
                    isGeneratingKey ||
                    (keyParams.keyType === 'Manual' && (!keyParams.p || !keyParams.q)) || 
                    !keyParams.signatureName
                  }
                  sx={{ minWidth: '100px' }}
                  startIcon={isGeneratingKey ? <CircularProgress size={20} color="inherit" /> : null}
                >
                  {isGeneratingKey ? 'Đang tạo...' : 'Tạo khóa'}
                </Button>
              )}
              <Button 
                variant="contained" 
                color="success"
                onClick={handleConfirm}
                disabled={!keyParams.publicKey || !keyParams.privateKey || !keyParams.signatureName}
                sx={{ minWidth: '100px' }}
              >
                {isEditMode ? 'Cập nhật' : 'Xác nhận'}
              </Button>
              <Button 
                variant="outlined" 
                onClick={handleCancel}
                color="error"
                sx={{ minWidth: '100px' }}
              >
                Hủy
              </Button>
            </DialogActions>
          </Dialog>

          {/* Import Key Confirmation Dialog */}
          <Dialog 
            open={openImportDialog} 
            onClose={handleImportCancel}
            maxWidth="sm"
            fullWidth
          >
            <DialogTitle>Xác nhận nhập khóa</DialogTitle>
            <DialogContent>
              {importKeyData && (
                <Box sx={{ mt: 2 }}>
                  <Typography variant="subtitle1" gutterBottom>
                    Bạn có chắc chắn muốn nhập khóa này?
                  </Typography>
                  
                  <Paper sx={{ p: 2, mt: 2, bgcolor: 'background.default' }}>
                    <Typography variant="body2" component="div">
                      <Box sx={{ mb: 1 }}><strong>Tên khóa:</strong> {importKeyData.signatureName || 'Không xác định'}</Box>
                      <Box sx={{ mb: 1 }}><strong>Loại khóa:</strong> {importKeyData.signatureType || 'Imported'}</Box>
                      <Box sx={{ mb: 1 }}><strong>Ngày xuất:</strong> {importKeyData.exportDate || 'Không xác định'}</Box>
                      
                      {importKeyData.p && importKeyData.q && (
                        <Box sx={{ mb: 1 }}><strong>Tham số:</strong> Có chứa p, q</Box>
                      )}
                    </Typography>
                  </Paper>
                  
                  <Alert severity="warning" sx={{ mt: 2 }}>
                    Chỉ nhập khóa từ nguồn đáng tin cậy! Khóa không hợp lệ có thể gây mất an toàn.
                  </Alert>
                </Box>
              )}
            </DialogContent>
            <DialogActions sx={{ justifyContent: 'center', pb: 2, gap: 2 }}>
              <Button 
                variant="contained" 
                color="primary"
                onClick={handleImportConfirm}
                sx={{ minWidth: '100px' }}
              >
                Xác nhận nhập
              </Button>
              <Button 
                variant="outlined" 
                onClick={handleImportCancel}
                color="error"
                sx={{ minWidth: '100px' }}
              >
                Hủy
              </Button>
            </DialogActions>
          </Dialog>

          {/* Import Progress Dialog */}
          <Dialog 
            open={isImporting} 
            maxWidth="sm"
            fullWidth
            disableEscapeKeyDown
          >
            <DialogTitle>Đang nhập khóa</DialogTitle>
            <DialogContent>
              <Box sx={{ 
                display: 'flex', 
                flexDirection: 'column',
                alignItems: 'center', 
                justifyContent: 'center',
                py: 3
              }}>
                {importProgress.step !== 'complete' ? (
                  <CircularProgress size={60} sx={{ mb: 2 }} />
                ) : (
                  <Box 
                    sx={{ 
                      width: 60, 
                      height: 60, 
                      borderRadius: '50%', 
                      bgcolor: 'success.main',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      mb: 2
                    }}
                  >
                    <Typography variant="h4" color="white">✓</Typography>
                  </Box>
                )}
                <Typography variant="h6" align="center">
                  {importProgress.message}
                </Typography>
              </Box>
            </DialogContent>
          </Dialog>

          {/* Notification */}
          <Snackbar 
            open={notification.open} 
            autoHideDuration={6000} 
            onClose={handleCloseNotification}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
          >
            <Alert onClose={handleCloseNotification} severity={notification.severity} sx={{ width: '100%' }}>
              {notification.message}
            </Alert>
          </Snackbar>
        </Box>
      </Container>
    </>
  );
};

export default SignatureManagement;