import React, { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import { useNavigate } from 'react-router-dom';
import {
  Box, Button, Paper, Typography, Chip, IconButton, MenuItem, Alert, AlertTitle, AppBar, Toolbar, Container,
  TextField, FormControl, InputLabel, Select, CircularProgress, FormControlLabel, Radio, RadioGroup,
  Tab, Tabs, Stepper, Step, StepLabel, Divider, Tooltip, Checkbox
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import LinkIcon from '@mui/icons-material/Link';
import FileUploadIcon from '@mui/icons-material/FileUpload';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import CreateIcon from '@mui/icons-material/Create';
import DescriptionIcon from '@mui/icons-material/Description';
import KeyIcon from '@mui/icons-material/Key';
import SettingsIcon from '@mui/icons-material/Settings';
import VerifiedUserIcon from '@mui/icons-material/VerifiedUser';
import TextFieldsIcon from '@mui/icons-material/TextFields';
import HelpOutlineIcon from '@mui/icons-material/HelpOutline';
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf';
import { useSnackbar } from 'notistack';
import { DownloadIcon } from 'lucide-react';

const API_URL = '/api';

interface Signature {
  id: string;
  publicKey: string;
  createdAt: string;
  signatureName: string;
  signatureType: string;
  isActive: boolean;
}

const Sign_file: React.FC = () => {
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { enqueueSnackbar } = useSnackbar();
  
  // Centralized notification function
  const showNotification = (message: string, severity: 'success' | 'error' | 'warning' | 'info') => {
    enqueueSnackbar(message, { variant: severity });
  };
  
  // States for signature selection
  const [signatures, setSignatures] = useState<Signature[]>([]);
  const [selectedSignId, setSelectedSignId] = useState<string>('');
  
  // States for sign method
  const [signMethod, setSignMethod] = useState<'manual' | 'file'>('manual');
  const [manualContent, setManualContent] = useState<string>('');
  const [fileContent, setFileContent] = useState<string>('');
  const [fileName, setFileName] = useState<string>('');
  const [fileToSign, setFileToSign] = useState<File | null>(null);
  
  // States for hash algorithm
  const [hashAlgorithm, setHashAlgorithm] = useState<'MD5' | 'SHA1' | 'SHA256' | 'SHA512'>('SHA256');
  
  // States for signing result
  const [signature, setSignature] = useState<string>('');
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Add state for embedded signature option
  const [useEmbeddedSign, setUseEmbeddedSign] = useState<boolean>(false);
  const [signedFileUrl, setSignedFileUrl] = useState<string>('');

  // Trong phần khai báo biến state, thêm state cho việc xem trước ảnh
  const [imagePreview, setImagePreview] = useState<string | null>(null);

  // Add state for PDF preview
  const [pdfPreview, setPdfPreview] = useState<string | null>(null);

  // Fetch signatures on component mount

  
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => {
    const userId = localStorage.getItem('userId');
    if (!userId) {
      showNotification('User not logged in. Please log in first.', 'error');
      return;
    }
    
    fetchSignatures(userId);
  }, []);

  // Fetch available signatures
  const fetchSignatures = async (userId: string) => {
    try {
      const response = await axios.get<{ signatures: Signature[] }>(`${API_URL}/sign/list/${userId}`);
      setSignatures(response.data.signatures);
      if (response.data.signatures.length > 0) {
        setSelectedSignId(response.data.signatures[0].id);
      }
    } catch (error) {
      console.error('Error fetching signatures:', error);
      showNotification('Failed to fetch signatures', 'error');
    }
  };

  // Handle file selection
  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    if (event.target.files && event.target.files.length > 0) {
      const file = event.target.files[0];
      setFileToSign(file);
      setFileName(file.name);
      setError(null);
      setImagePreview(null); // Reset image preview
      setPdfPreview(null); // Reset PDF preview
      
      // Giới hạn kích thước file
      if (file.size > 10 * 1024 * 1024) { // 10MB
        setError('Kích thước file vượt quá giới hạn 10MB');
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
      } else if (file.type.includes('pdf') || 
                file.name.endsWith('.pdf')) {
        // Đối với PDF, tạo URL để xem trước
        const pdfUrl = URL.createObjectURL(file);
        setPdfPreview(pdfUrl);
        setFileContent(`[PDF Document] - ${file.name} (${(file.size / 1024).toFixed(2)} KB)`);
        showNotification('Đã tải file PDF thành công', 'success');
      } else if (file.type.includes('word') || 
                file.name.endsWith('.doc') || 
                file.name.endsWith('.docx')) {
        // Đối với Word, hiển thị thông tin
        setFileContent(`[Word Document] - ${file.name} (${(file.size / 1024).toFixed(2)} KB)`);
        showNotification('Đã tải file Word thành công', 'success');
      } else if (file.type.includes('excel') || 
                file.name.endsWith('.xls') || 
                file.name.endsWith('.xlsx')) {
        // Đối với Excel, hiển thị thông tin
        setFileContent(`[Excel Document] - ${file.name} (${(file.size / 1024).toFixed(2)} KB)`);
        showNotification('Đã tải file Excel thành công', 'success');
      } else if (file.type.includes('image/') ||
                ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp'].some(ext => 
                  file.name.toLowerCase().endsWith(ext))) {
        // Xử lý file ảnh
        const reader = new FileReader();
        reader.onload = (e) => {
          try {
            const content = e.target?.result as string;
            setImagePreview(content);
            setFileContent(`[Image] - ${file.name} (${(file.size / 1024).toFixed(2)} KB)`);
            showNotification('Đã tải ảnh thành công', 'success');
          } catch (error) {
            console.error('Error reading image file:', error);
            setFileContent('Không thể đọc nội dung ảnh.');
            showNotification('Không thể đọc nội dung ảnh', 'error');
          }
        };
        reader.onerror = () => {
          setFileContent('Lỗi: Không thể đọc file.');
          showNotification('Lỗi đọc file', 'error');
        };
        reader.readAsDataURL(file);
      } else {
        // Kiểm tra xem có phải file có thể ký không
        const validExtensions = ['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.txt', '.csv', '.json', '.md', '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp'];
        const extension = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
        
        if (!validExtensions.includes(extension)) {
          setError(`Định dạng file không được hỗ trợ. Hỗ trợ: ${validExtensions.join(', ')}`);
          showNotification(`Định dạng file không được hỗ trợ. Vui lòng chọn file khác.`, 'error');
          setFileToSign(null);
          setFileName('');
          setFileContent('');
          return;
        }
        
        // Đối với các định dạng khác được hỗ trợ
        setFileContent(`[File] - ${file.name} (${(file.size / 1024).toFixed(2)} KB)\nSẵn sàng để ký.`);
        showNotification('Đã tải file thành công', 'success');
      }
    }
  };

  // Handle signing of manual text
  const handleSignManualText = async () => {
    if (!manualContent) {
      showNotification('Vui lòng nhập nội dung cần ký', 'error');
      return;
    }
    
    if (!selectedSignId) {
      showNotification('Vui lòng chọn chữ ký', 'error');
      return;
    }
    
    setIsLoading(true);
    setError(null);
    
    try {
      const userId = localStorage.getItem('userId');
      if (!userId) {
        showNotification('Bạn chưa đăng nhập', 'error');
        setIsLoading(false);
        return;
      }
      
      // Get the signature details
      const signatureResponse = await axios.get(`${API_URL}/manualsign/get-key-details/${userId}/${selectedSignId}`);
      
      if (!signatureResponse.data || !signatureResponse.data.signature) {
        showNotification('Không thể lấy thông tin chữ ký', 'error');
        setIsLoading(false);
        return;
      }
      
      const signature = signatureResponse.data.signature;
      
      // Check if the signature is RSA2048 or larger
      const isRsa2048OrLarger = 
        signature.signatureType.includes('Rsa') && 
        parseInt(signature.signatureType.replace('Rsa', '')) >= 2048;
      
      // Determine signing method based on signature type
      if (isRsa2048OrLarger && signature.privateKey) {
        // Use the new private key RSA signing endpoint for RSA2048 and larger
        try {
          const response = await axios.post(`${API_URL}/manualsign/sign-with-private-key-rsa`, {
            privateKey: signature.privateKey,
            data: manualContent,
            signatureName: signature.signatureName,
            userId,
            hashAlgorithm: hashAlgorithm
          });
          
          if (response.data && response.data.signature) {
            setSignature(response.data.signature);
            showNotification('Ký văn bản thành công', 'success');
          } else {
            showNotification('Ký văn bản thất bại', 'error');
          }
        } catch (error: unknown) {
          console.error('Error signing with private key RSA:', error);
          if (axios.isAxiosError(error)) {
            const errorMessage = error.response?.data || error.message;
            setError(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`);
            showNotification(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`, 'error');
          } else {
            setError(`Lỗi ký văn bản: ${String(error)}`);
            showNotification(`Lỗi ký văn bản: ${String(error)}`, 'error');
          }
        }
      } else {
        // Fallback to existing manual signing methods
        // Existing code for other signature types...
        const isManualSignature = 
          signature.signatureType === 'Manual' || 
          signature.signatureType === 'Auto' || 
          (signature.publicKey && signature.publicKey.startsWith('{'));
        
        if (isManualSignature) {
          // Existing manual signature handling code...
          try {
            // Lấy n, e, d từ publicKey và privateKey dạng JSON
            let publicKeyObj: { n?: string; e?: string } = {};
            // Parsed RSA public key components
            let privateKeyObj: { n?: string; d?: string } = {};
            // Parsed RSA private key components
            let n = '';
            let e = '';
            let d = '';
            
            try {
              if (signature.publicKey && signature.publicKey.startsWith('{')) {
                publicKeyObj = JSON.parse(signature.publicKey);
                console.log('Parsed public key:', publicKeyObj);
                if (publicKeyObj.n && publicKeyObj.e) {
                  n = publicKeyObj.n;
                  e = publicKeyObj.e;
                }
              }
              
              if (signature.privateKey && signature.privateKey.startsWith('{')) {
                privateKeyObj = JSON.parse(signature.privateKey);
                console.log('Parsed private key:', privateKeyObj);
                if (privateKeyObj.n && privateKeyObj.d) {
                  d = privateKeyObj.d;
                  // Sử dụng n từ privateKey nếu không có trong publicKey
                  if (!n) n = privateKeyObj.n;
                }
              }
              
              console.log('Extracted values:', { n, e, d });
            } catch (error) {
              console.error('Lỗi parse JSON key:', error);
              showNotification('Lỗi định dạng khóa', 'error');
              setIsLoading(false);
              return;
            }
            
            if (!n || !e || !d) {
              console.log('Using p, q, e, d from signature:', { 
                p: signature.p, 
                q: signature.q, 
                e: signature.e, 
                d: signature.d 
              });
              
              // Sử dụng p, q, e, d nếu có
              if (signature.p && signature.q && signature.e && signature.d) {
                // Sử dụng tham số p,q,e,d đã lưu
                console.log('Signing with p, q, e, d');
                const response = await axios.post(`${API_URL}/manualsign/sign-from-params`, {
                  p: signature.p,
                  q: signature.q,
                  e: signature.e,
                  d: signature.d,
                  data: manualContent,
                  signatureName: signature.signatureName,
                  userId,
                  hashAlgorithm
                });
                
                console.log('Sign response:', response.data);
                
                if (response.data && response.data.signature) {
                  setSignature(response.data.signature);
                  showNotification('Ký văn bản thành công', 'success');
                } else {
                  showNotification('Ký văn bản thất bại', 'error');
                }
              } else {
                showNotification('Chữ ký không có đủ thông tin n, e, d', 'error');
                setIsLoading(false);
                return;
              }
            } else {
              // Sử dụng n, e, d cho ký
              console.log('Signing with n, e, d');
              try {
                const response = await axios.post(`${API_URL}/manualsign/sign-with-ned`, {
                  n,
                  e,
                  d,
                  data: manualContent,
                  signatureName: signature.signatureName,
                  userId,
                  hashAlgorithm
                });
                
                console.log('Sign response:', response.data);
                
                if (response.data && response.data.signature) {
                  setSignature(response.data.signature);
                  showNotification('Ký văn bản thành công', 'success');
                } else {
                  showNotification('Ký văn bản thất bại', 'error');
                }
              } catch (error) {
                console.error('Error in sign-with-ned API call:', error);
                if (axios.isAxiosError(error)) {
                  const errorMessage = error.response?.data || error.message;
                  setError(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`);
                  showNotification(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`, 'error');
                } else {
                  setError(`Lỗi ký văn bản: ${String(error)}`);
                  showNotification(`Lỗi ký văn bản: ${String(error)}`, 'error');
                }
              }
            }
          } catch (error: unknown) {
            console.error('Error signing text with n,e,d:', error);
            if (axios.isAxiosError(error) && error.response) {
              const errorMessage = error.response.data || error.message;
              setError(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`);
              showNotification(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`, 'error');
            } else {
              setError(`Lỗi ký văn bản: ${String(error)}`);
              showNotification(`Lỗi ký văn bản: ${String(error)}`, 'error');
            }
          }
        } else {
          // Existing code for standard RSA signatures...
          try {
            // Existing code for signing with base64 private key...
          } catch (error: unknown) {
            console.error('Error signing text with private key:', error);
            if (axios.isAxiosError(error) && error.response) {
              const errorMessage = error.response.data || error.message;
              setError(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`);
              showNotification(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`, 'error');
            } else {
              setError(`Lỗi ký văn bản: ${String(error)}`);
              showNotification(`Lỗi ký văn bản: ${String(error)}`, 'error');
            }
          }
        }
      }
    } catch (error: unknown) {
      console.error('Error signing text:', error);
      if (axios.isAxiosError(error) && error.response) {
        const errorMessage = error.response.data || error.message;
        setError(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`);
        showNotification(`Lỗi ký văn bản: ${JSON.stringify(errorMessage)}`, 'error');
      } else {
        setError(`Lỗi ký văn bản: ${String(error)}`);
        showNotification(`Lỗi ký văn bản: ${String(error)}`, 'error');
      }
    } finally {
      setIsLoading(false);
    }
  };

  // Centralized error logging function
  const logError = (context: string, error: unknown): string => {
  
    console.error(`[${context}] Error details:`, error);
    if (axios.isAxiosError(error)) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      return (error.response?.data as any)?.message || error.message || 'Lỗi không xác định khi thực hiện thao tác';
    } else if (error instanceof Error) {
      return error.message;
    } else {
      return 'Lỗi không xác định khi thực hiện thao tác';
    }
  };


  // Handle signing of file
  const handleSignFile = async () => {
    if (!fileToSign) {
      showNotification('Vui lòng chọn file để ký', 'error');
      return;
    }
    
    if (!selectedSignId) {
      showNotification('Vui lòng chọn chữ ký', 'error');
      return;
    }
    
    // Kiểm tra kích thước file
    if (fileToSign.size > 10 * 1024 * 1024) { // 10MB
      setError('Kích thước file vượt quá giới hạn 10MB');
      showNotification('Kích thước file vượt quá giới hạn 10MB', 'error');
      return;
    }
    
    setIsLoading(true);
    setError(null);
    
    try {
      const userId = localStorage.getItem('userId');
      if (!userId) {
        showNotification('Bạn chưa đăng nhập', 'error');
        setIsLoading(false);
        return;
      }
      
      // Create a FormData object to send the file
      const formData = new FormData();
      formData.append('file', fileToSign);
      formData.append('userId', userId);
      formData.append('signatureId', selectedSignId);
      formData.append('hashAlgorithm', hashAlgorithm);
      
      console.log('Sending file signing request with FormData:', {
        fileName: fileToSign.name,
        fileSize: fileToSign.size,
        userId,
        signatureId: selectedSignId,
        hashAlgorithm
      });

      try {
        const response = await axios.post(`${API_URL}/manualsign/sign-file`, formData, {
          headers: {
            
          },
          timeout: 30000 
        });
        
        console.log('File signing response received:', response.data);
        
        if (response.data && response.data.signature) {
          // Get the public key for the signature
          const signatureResponse = await axios.get(`${API_URL}/manualsign/get-key-details/${userId}/${selectedSignId}`);
          const publicKey = signatureResponse.data?.signature?.publicKey || '';
          
          showNotification('Ký file thành công!', 'success');
        
          // Show signature and public key
          setSignature(response.data.signature);
          
          // Create UI elements for downloading signature and public key
          const signatureBlob = new Blob([response.data.signature], { type: 'text/plain' });
          const signatureUrl = URL.createObjectURL(signatureBlob);
          const signatureLink = document.createElement('a');
          signatureLink.href = signatureUrl;
          signatureLink.download = `${fileToSign.name}_signature.txt`;
          
          if (publicKey) {
            const publicKeyBlob = new Blob([publicKey], { type: 'text/plain' });
            const publicKeyUrl = URL.createObjectURL(publicKeyBlob);
            const publicKeyLink = document.createElement('a');
            publicKeyLink.href = publicKeyUrl;
            publicKeyLink.download = `${fileToSign.name}_public_key.txt`;
          }
        } else {
          showNotification('Không nhận được chữ ký từ máy chủ', 'error');
        }
      } catch (error: unknown) {
        // Centralized error handling
        const userErrorMessage = logError('File Signing', error);
        
        // Specific error type handling
        if (axios.isAxiosError(error)) {
          if (error.response?.status === 413) {
            showNotification('File quá lớn. Vui lòng chọn file nhỏ hơn 10MB', 'error');
          } else if (error.response?.status === 401) {
            showNotification('Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại', 'error');
          } else {
            showNotification(`Lỗi ký file: ${userErrorMessage}`, 'error');
          }
        } else {
          showNotification(`Lỗi không xác định: ${userErrorMessage}`, 'error');
        }
      }
    } catch (generalError) {
      // Catch any unexpected errors
      const errorMessage = logError('General File Signing', generalError);
      showNotification(`Lỗi hệ thống: ${errorMessage}`, 'error');
    } finally {
      setIsLoading(false);
    }
  };

  // Handle signing of file with embedded signature
  const handleSignFileEmbedded = async () => {
    if (!fileToSign) {
      showNotification('Vui lòng chọn file để ký', 'error');
      return;
    }
    
    if (!selectedSignId) {
      showNotification('Vui lòng chọn chữ ký', 'error');
      return;
    }
    
    // Kiểm tra kích thước file
    if (fileToSign.size > 10 * 1024 * 1024) { // 10MB
      setError('Kích thước file vượt quá giới hạn 10MB');
      showNotification('Kích thước file vượt quá giới hạn 10MB', 'error');
      return;
    }
    
    setIsLoading(true);
    setError(null);
    
    try {
      const userId = localStorage.getItem('userId');
      if (!userId) {
        showNotification('Bạn chưa đăng nhập', 'error');
          setIsLoading(false);
          return;
        }
        
      // Create a FormData object for embedded signing
      const formData = new FormData();
      formData.append('File', fileToSign, fileToSign.name);
      formData.append('UserId', userId); // lowercase
      formData.append('SignId', selectedSignId); // lowercase
      formData.append('HashAlgorithm', hashAlgorithm); // lowercase
      formData.append('UseEmbeddedSign', 'true'); // lowercase
      
      console.log('Sending embedded file signing request with FormData:', {
        fileName: fileToSign.name,
        fileSize: fileToSign.size,
        userid: userId,
        signid: selectedSignId,
        hashalgorithm: hashAlgorithm,
        useembeddedsign: true
      });
      
      // Use the sign-document endpoint for embedded signing
      try {
        console.log('Making API request to /sign/sign-document with FormData');
        
        // Use a different approach to handle binary responses
        const response = await axios.post(`${API_URL}/sign/sign-document`, formData, {
          headers: {
            
          },
          responseType: 'blob', // Important for binary data
          timeout: 60000 // Increase timeout to 60 seconds
        });
        
        console.log('Embedded file signing response received', {
          status: response.status,
          statusText: response.statusText,
          headers: response.headers,
          contentType: response.headers['content-type'],
          contentDisposition: response.headers['content-disposition']
        });
        
        // Create a download link for the signed file
        const blob = new Blob([response.data], { 
          type: response.headers['content-type'] || 'application/pdf'
        });
        const url = window.URL.createObjectURL(blob);
        setSignedFileUrl(url);
        
        // Get the filename from response headers
        const contentDisposition = response.headers['content-disposition'];
        let signedFileName = 'signed_document.pdf';
        
        if (contentDisposition) {
          console.log('Content-Disposition header:', contentDisposition);
          const fileNameMatch = contentDisposition.match(/filename="?([^"]*)"?/);
          if (fileNameMatch && fileNameMatch[1]) {
            signedFileName = fileNameMatch[1];
            console.log('Extracted filename:', signedFileName);
          }
        }
        
        showNotification('Ký file thành công! Tệp đã được ký nhúng.', 'success');
        
        // Automatically download the file
        const link = document.createElement('a');
        link.href = url;
        link.download = signedFileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
      } catch (error: unknown) {
        console.error('Error during embedded file signing:', error);
        console.log('Full error object:', JSON.stringify(error, null, 2));
        
        let errorMessage = 'Lỗi ký file';
        
        // If the response is a blob, we need to read it as text
        if (axios.isAxiosError(error) && error.response && error.response.data instanceof Blob) {
          console.log('Error response is a blob. Trying to read as text...');
          try {
            const textContent = await error.response.data.text();
            console.log('Error blob content (raw):', textContent);
              try {
              const errorObj = JSON.parse(textContent);
              errorMessage = errorObj.message || errorObj.error || 'Lỗi ký file';
              console.log('Parsed error object:', errorObj);
              } catch {
              console.log('Error is not valid JSON, using as plain text');
              errorMessage = textContent || 'Lỗi ký file';
            }
          } catch (readError) {
            console.error('Error reading blob error response:', readError);
          }
        } 
        // Check for Axios error response
        else if (axios.isAxiosError(error) && error.response) {
          console.log('Error response status:', error.response.status);
          console.log('Error response headers:', error.response.headers);
          
          if (error.response.data) {
            console.log('Error response data type:', typeof error.response.data);
            if (typeof error.response.data === 'string') {
              errorMessage = error.response.data;
            } else if (typeof error.response.data === 'object') {
              console.log('Error response data:', error.response.data);
          errorMessage = error.response.data.message || 
                         error.response.data.error || 
                            JSON.stringify(error.response.data);
            }
          } else {
            errorMessage = `Lỗi HTTP ${error.response.status}: ${error.response.statusText}`;
          }
        }
        // Fallback to generic error message
        else {
          errorMessage = 'Lỗi không xác định khi ký file';
          }
        
        // Set error state and show notification
        console.log('Final error message:', errorMessage);
        setError(`Lỗi ký file: ${errorMessage}`);
        showNotification(`Lỗi ký file: ${errorMessage}`, 'error');
      }
    } catch (error) {
      console.error('General error in handleSignFileEmbedded:', error);
      setError(`Lỗi ký file: ${(error as Error).message || 'Không xác định'}`);
      showNotification(`Lỗi ký file: ${(error as Error).message || 'Không xác định'}`, 'error');
    } finally {
      setIsLoading(false);
    }
  };

  // Handle file signing with appropriate method based on embedding preference
  const handleSignFileWithMethod = () => {
    if (useEmbeddedSign) {
      handleSignFileEmbedded();
    } else {
      handleSignFile();
    }
  };

  // Copy signature to clipboard
  const copyToClipboard = () => {
    navigator.clipboard.writeText(signature)
      .then(() => showNotification('Signature copied to clipboard', 'success'))
      .catch(() => showNotification('Failed to copy signature', 'error'));
  };

  // Go back to dashboard
  const goToDashboard = () => {
    navigate('/homepage');
  };

  // Function to download manual text as TXT file
  const downloadManualTextAsFile = () => {
    if (!manualContent) {
      showNotification('Không có nội dung văn bản để tải xuống', 'error');
      return;
    }
    
    // Create a Blob with the text content
    const textBlob = new Blob([manualContent], { type: 'text/plain;charset=utf-8' });
    const textUrl = URL.createObjectURL(textBlob);
    
    // Create a download link and trigger it
    const downloadLink = document.createElement('a');
    downloadLink.href = textUrl;
    downloadLink.download = `van_ban_${new Date().getTime()}.txt`;
    document.body.appendChild(downloadLink);
    downloadLink.click();
    
    // Clean up
    window.URL.revokeObjectURL(textUrl);
    document.body.removeChild(downloadLink);
    
    showNotification('Đã tải xuống văn bản thành công (.txt)', 'success');
  };
  
  // Add these functions for exporting signature and public key
  const exportSignatureAsFile = () => {
    if (!signature) {
      showNotification('Không có chữ ký để xuất', 'error');
      return;
    }
    // Save as .sig (raw Base64)
    const sigBlob = new Blob([signature], { type: 'application/octet-stream' });
    const sigUrl = URL.createObjectURL(sigBlob);
    const sigLink = document.createElement('a');
    sigLink.href = sigUrl;
    sigLink.download = `${fileName ? fileName.replace(/\.[^/.]+$/, '') : 'signature'}_${new Date().getTime()}.sig`;
    document.body.appendChild(sigLink);
    sigLink.click();
    window.URL.revokeObjectURL(sigUrl);
    document.body.removeChild(sigLink);
    showNotification('Đã xuất chữ ký thành công (.sig)', 'success');
  };
  
  const exportPublicKeyAsFile = async () => {
    if (!selectedSignId) {
      showNotification('Vui lòng chọn chữ ký', 'error');
      return;
    }
    try {
      const userId = localStorage.getItem('userId');
      const response = await axios.get(`${API_URL}/manualsign/get-key-details/${userId}/${selectedSignId}`);
      if (response.data && response.data.signature && response.data.signature.publicKey) {
        const publicKey = response.data.signature.publicKey;
        // If the key is JSON, pretty print it, else export as is
        let pemContent = '';
        try {
          const keyObj = JSON.parse(publicKey);
          pemContent = JSON.stringify(keyObj, null, 2);
        } catch {
          pemContent = publicKey;
        }
        // Save as .pem
        const pemBlob = new Blob([pemContent], { type: 'application/x-pem-file' });
        const pemUrl = URL.createObjectURL(pemBlob);
        const pemLink = document.createElement('a');
        pemLink.href = pemUrl;
        pemLink.download = `public_key_${new Date().getTime()}.pem`;
        document.body.appendChild(pemLink);
        pemLink.click();
        window.URL.revokeObjectURL(pemUrl);
        document.body.removeChild(pemLink);
        showNotification('Đã xuất khóa công khai thành công (.pem)', 'success');
      } else {
        showNotification('Không thể lấy khóa công khai', 'error');
      }
    } catch (error) {
      console.error('Error exporting public key:', error);
      showNotification('Lỗi khi xuất khóa công khai', 'error');
    }
  };

  return (
    <Box sx={{ flexGrow: 1 }}>
      <AppBar position="static">
        <Toolbar>
          <LinkIcon sx={{ mr: 1 }} />
          <Typography 
            variant="h6" 
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
      
      <Container maxWidth="md" sx={{ mt: 4 }}>
        <Paper sx={{ p: 3, mb: 3, borderRadius: 2, boxShadow: 3 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
            <CreateIcon sx={{ fontSize: 28, color: 'primary.main', mr: 1 }} />
            <Typography variant="h5" sx={{ fontWeight: 'bold' }}>
              Ký số tài liệu
            </Typography>
          </Box>
          
          <Stepper activeStep={signMethod === 'manual' ? 0 : 1} alternativeLabel sx={{ mb: 4 }}>
            <Step completed={Boolean(selectedSignId)}>
              <StepLabel icon={<KeyIcon color={selectedSignId ? "primary" : "disabled"} />}>Chọn chữ ký</StepLabel>
            </Step>
            <Step completed={signMethod === 'manual' ? Boolean(manualContent) : Boolean(fileToSign)}>
              <StepLabel icon={<DescriptionIcon color={(signMethod === 'manual' ? Boolean(manualContent) : Boolean(fileToSign)) ? "primary" : "disabled"} />}>Chọn văn bản</StepLabel>
            </Step>
            <Step completed={Boolean(signature || signedFileUrl)}>
              <StepLabel icon={<CreateIcon color={(signature || signedFileUrl) ? "primary" : "disabled"} />}>Ký tài liệu</StepLabel>
            </Step>
          </Stepper>
          
          {error && (
            <Alert severity="error" sx={{ my: 2 }}>
              <AlertTitle>Lỗi</AlertTitle>
              {error}
            </Alert>
          )}
          
          <Paper 
            elevation={0} 
            sx={{ 
              mt: 2, 
              p: 2, 
              bgcolor: '#f8f9fa', 
              borderRadius: 2,
              border: '1px solid #e0e0e0'
            }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <DescriptionIcon sx={{ color: 'primary.main', mr: 1 }} />
              <Typography variant="h6" sx={{ fontWeight: 'bold' }}>
                Bước 1: Chọn văn bản
              </Typography>
              <Tooltip title="Chọn văn bản để ký số. Bạn có thể nhập trực tiếp hoặc tải file.">
                <HelpOutlineIcon sx={{ ml: 1, fontSize: 18, color: 'text.secondary' }} />
              </Tooltip>
            </Box>
            
          <Tabs
            value={signMethod}
              onChange={(_, value: 'manual' | 'file') => setSignMethod(value)}
            variant="fullWidth"
            indicatorColor="primary"
            textColor="primary"
              sx={{ mb: 3, bgcolor: '#f5f5f5', borderRadius: 1 }}
          >
              <Tab value="manual" label="Ký văn bản thủ công" icon={<TextFieldsIcon />} iconPosition="start" />
              <Tab value="file" label="Ký file" icon={<FileUploadIcon />} iconPosition="start" />
          </Tabs>
          
          {signMethod === 'manual' ? (
            <Box sx={{ position: 'relative', mb: 3 }}>
              <TextField
                label="Nội dung cần ký"
                multiline
                rows={6}
                fullWidth
                variant="outlined"
                value={manualContent}
                onChange={(e) => setManualContent(e.target.value)}
                placeholder="Nhập nội dung văn bản cần ký số..."
              />
              {manualContent && (
                <Tooltip title="Tải xuống nội dung dưới dạng file TXT">
                  <Button
                    variant="outlined"
                    size="small"
                    color="primary"
                    startIcon={<DownloadIcon />}
                    onClick={downloadManualTextAsFile}
                    sx={{ 
                      position: 'absolute', 
                      right: '10px', 
                      bottom: '10px',
                      bgcolor: 'rgba(255, 255, 255, 0.9)',
                      '&:hover': {
                        bgcolor: 'rgba(255, 255, 255, 1)'
                      }
                    }}
                  >
                    Tải TXT
                  </Button>
                </Tooltip>
              )}
            </Box>
          ) : (
              <Box sx={{ mb: 3, p: 2, border: '1px dashed #ccc', borderRadius: 2, bgcolor: '#fafafa' }}>
              <input
                type="file"
                ref={fileInputRef}
                style={{ display: 'none' }}
                onChange={handleFileSelect}
                accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.md,.json,.csv,.jpg,.jpeg,.png,.gif,.bmp,.tiff,.tif,.ico,.webp"
              />
                <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', py: 2 }}>
                  <FileUploadIcon sx={{ fontSize: 48, color: 'primary.main', mb: 1 }} />
              <Button
                variant="contained"
                startIcon={<FileUploadIcon />}
                onClick={() => fileInputRef.current?.click()}
                    sx={{ mb: 1 }}
              >
                Chọn file hoặc ảnh để ký
              </Button>
                  <Typography variant="body2" color="textSecondary">
                    Hỗ trợ: PDF, Word, Excel, Text, và các định dạng ảnh (JPG, PNG, GIF...)
                  </Typography>
                </Box>
              
              {fileName && (
                  <Paper variant="outlined" sx={{ p: 2, mt: 2, bgcolor: '#f5f5f5', display: 'flex', alignItems: 'center' }}>
                    <DescriptionIcon sx={{ mr: 1, color: 'success.main' }} />
                    <Box>
                      <Typography variant="body1" sx={{ fontWeight: 'bold' }}>
                        {fileName}
                      </Typography>
                      {fileContent && (
                        <Typography variant="body2" color="textSecondary">
                          Sẵn sàng để ký
                </Typography>
                      )}
                    </Box>
                  </Paper>
              )}
              
              {/* PDF Preview */}
              {pdfPreview && (
                <Paper variant="outlined" sx={{ p: 2, mt: 2, bgcolor: '#f8f8f8', textAlign: 'center' }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                    <PictureAsPdfIcon sx={{ mr: 1, color: 'error.main' }} />
                    <Typography variant="subtitle1" sx={{ fontWeight: 'bold' }}>
                      Xem trước PDF:
                    </Typography>
                    <Chip 
                      label="Xem trước PDF"
                      size="small" 
                      color="error" 
                      variant="outlined"
                      sx={{ ml: 1, fontSize: '0.7rem' }}
                    />
                  </Box>
                  <Box 
                    sx={{ 
                      width: '100%',
                      height: '500px',
                      maxHeight: '500px',
                      overflow: 'hidden',
                      border: '1px solid #e0e0e0',
                      borderRadius: '4px'
                    }}
                  >
                    <iframe 
                      src={`${pdfPreview}#toolbar=0&navpanes=0`}
                      width="100%" 
                      height="100%" 
                      style={{ border: 'none' }} 
                      title="PDF Preview"
                    />
                  </Box>
                  <Box sx={{ mt: 1, display: 'flex', justifyContent: 'flex-end' }}>
                    <Button 
                      variant="outlined" 
                      size="small" 
                      startIcon={<PictureAsPdfIcon />}
                      onClick={() => window.open(pdfPreview, '_blank')}
                    >
                      Mở PDF trong tab mới
                    </Button>
                  </Box>
                </Paper>
              )}
              
              {/* Image Preview - existing code */}
              {imagePreview && (
                <Paper variant="outlined" sx={{ p: 2, mt: 2, bgcolor: '#f8f8f8', textAlign: 'center' }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                    <Typography variant="subtitle1" sx={{ fontWeight: 'bold' }}>
                      Xem trước ảnh:
                    </Typography>
                    <Chip 
                      label="Xem trước ảnh"
                      size="small" 
                      color="primary" 
                      variant="outlined"
                      sx={{ ml: 1, fontSize: '0.7rem' }}
                    />
                  </Box>
                  <Box 
                    sx={{ 
                      maxWidth: '100%',
                      maxHeight: '300px',
                      overflow: 'auto',
                      display: 'flex',
                      justifyContent: 'center'
                    }}
                  >
                    <img 
                      src={imagePreview} 
                      alt="Preview" 
                      style={{ 
                        maxWidth: '100%', 
                        maxHeight: '300px', 
                        objectFit: 'contain',
                        border: '1px solid #e0e0e0',
                        borderRadius: '4px'
                      }} 
                    />
                  </Box>
                </Paper>
              )}
              
              {/* Text content preview - existing code with condition to not show if PDF or image preview is active */}
              {fileContent && !imagePreview && !pdfPreview && (
                  <Paper variant="outlined" sx={{ p: 2, mt: 2, bgcolor: '#f8f8f8', maxHeight: '300px', overflow: 'auto' }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                      <Typography variant="subtitle1" sx={{ fontWeight: 'bold' }}>
                    Xem trước nội dung:
                  </Typography>
                      <Chip 
                        label="Xem trước nội dung văn bản"
                        size="small" 
                        color="primary" 
                    variant="outlined"
                        sx={{ ml: 1, fontSize: '0.7rem' }}
                      />
                    </Box>
                    <Box 
                    sx={{ 
                        whiteSpace: 'pre-wrap', 
                        fontFamily: 'monospace',
                        fontSize: '0.9rem',
                        p: 1,
                        border: '1px solid #e0e0e0',
                        borderRadius: 1,
                        backgroundColor: '#fff',
                        maxHeight: '250px',
                        overflow: 'auto'
                      }}
                    >
                      {fileContent}
                </Box>
                  </Paper>
              )}
            </Box>
          )}
        </Paper>
        
          <Paper 
            elevation={0} 
            sx={{ 
              mt: 3, 
              p: 2, 
              bgcolor: '#f8f9fa', 
              borderRadius: 2,
              border: '1px solid #e0e0e0'
            }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <KeyIcon sx={{ color: 'primary.main', mr: 1 }} />
              <Typography variant="h6" sx={{ fontWeight: 'bold' }}>
                Bước 2: Chọn chữ ký
          </Typography>
              <Tooltip title="Chọn chữ ký số để ký văn bản. Bạn cần tạo chữ ký trước trong phần Quản lý chữ ký.">
                <HelpOutlineIcon sx={{ ml: 1, fontSize: 18, color: 'text.secondary' }} />
              </Tooltip>
            </Box>
          
            <FormControl fullWidth variant="outlined" sx={{ mb: 3 }}>
            <InputLabel>Chọn chữ ký</InputLabel>
            <Select
              value={selectedSignId}
              onChange={(e) => setSelectedSignId(e.target.value as string)}
              label="Chọn chữ ký"
                startAdornment={<KeyIcon sx={{ mr: 1, color: 'action.active' }} />}
            >
              <MenuItem value="" disabled>
                <em>Chọn chữ ký</em>
              </MenuItem>
              {signatures.map((sig) => (
                <MenuItem key={sig.id} value={sig.id}>
                  {sig.signatureName} ({sig.signatureType})
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          
            <Box sx={{ p: 2, bgcolor: '#f8f9fa', borderRadius: 2, mb: 3 }}>
              <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 'bold', display: 'flex', alignItems: 'center' }}>
                <SettingsIcon sx={{ mr: 1, fontSize: 18, color: 'primary.main' }} />
                Cài đặt ký số:
                <Tooltip title="Thuật toán băm được sử dụng để tạo giá trị băm từ nội dung trước khi ký">
                  <HelpOutlineIcon sx={{ ml: 1, fontSize: 16, color: 'text.secondary' }} />
                </Tooltip>
              </Typography>
              
              <FormControl component="fieldset" sx={{ ml: 4 }}>
                <Typography variant="subtitle2" gutterBottom>
              Thuật toán băm:
            </Typography>
            <RadioGroup 
              row 
              aria-label="hash-algorithm" 
              name="hash-algorithm" 
              value={hashAlgorithm}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => setHashAlgorithm(e.target.value as 'MD5' | 'SHA1' | 'SHA256' | 'SHA512')}
            >
                  <FormControlLabel value="MD5" control={<Radio size="small" />} label={
                    <Tooltip title="MD5 - Nhanh nhưng kém bảo mật hơn">
                      <Box component="span">MD5</Box>
                    </Tooltip>
                  } />
                  <FormControlLabel value="SHA1" control={<Radio size="small" />} label={
                    <Tooltip title="SHA-1 - Cân bằng giữa tốc độ và bảo mật">
                      <Box component="span">SHA-1</Box>
                    </Tooltip>
                  } />
                  <FormControlLabel value="SHA256" control={<Radio size="small" />} label={
                    <Tooltip title="SHA-256 - Bảo mật cao, được khuyến nghị sử dụng">
                      <Box component="span">SHA-256</Box>
                    </Tooltip>
                  } />
                  <FormControlLabel value="SHA512" control={<Radio size="small" />} label={
                    <Tooltip title="SHA-512 - Bảo mật cao nhất, nhưng chậm hơn">
                      <Box component="span">SHA-512</Box>
                    </Tooltip>
                  } />
            </RadioGroup>
          </FormControl>
            </Box>

            {/* Add embedded signature option for file signing */}
            {signMethod === 'file' && (
              <Box sx={{ mt: 2, p: 2, bgcolor: '#f0f7ff', borderRadius: 2 }}>
                <Box sx={{ display: 'flex', alignItems: 'center' }}>
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={useEmbeddedSign}
                        onChange={(e) => setUseEmbeddedSign(e.target.checked)}
                        color="primary"
                      />
                    }
                    label={
                      <Box sx={{ display: 'flex', alignItems: 'center' }}>
                        <Typography variant="body1" sx={{ fontWeight: 'medium' }}>
                          Sử dụng ký nhúng (embedded)
                        </Typography>
                        <Tooltip title="Ký nhúng sẽ tạo ra file PDF đã được ký trực tiếp, không cần file chữ ký riêng. Chữ ký được nhúng vào bên trong tài liệu và có thể được xác thực bởi các phần mềm đọc PDF thông thường.">
                          <HelpOutlineIcon sx={{ ml: 1, fontSize: 16, color: 'text.secondary' }} />
                        </Tooltip>
                      </Box>
                    }
                  />
                </Box>
                {useEmbeddedSign && (
                  <Alert severity="info" sx={{ mt: 2 }}>
                    <AlertTitle>Lưu ý về chữ ký nhúng</AlertTitle>
                    Chữ ký nhúng yêu cầu sử dụng khóa RSA2048 trở lên. Các khóa có độ dài thấp hơn hoặc khóa thủ công không thể sử dụng cho chữ ký nhúng.
                  </Alert>
                )}
              </Box>
            )}
          </Paper>
          
          <Paper 
            elevation={0} 
            sx={{ 
              mt: 3, 
              p: 2, 
              bgcolor: '#f0f7ff', 
              borderRadius: 2,
              border: '1px solid #c2e0ff'
            }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <CreateIcon sx={{ color: 'primary.main', mr: 1 }} />
              <Typography variant="h6" sx={{ fontWeight: 'bold' }}>
                Bước 3: Ký tài liệu
              </Typography>
              <Tooltip title="Nhấn nút để ký tài liệu bằng chữ ký số đã chọn">
                <HelpOutlineIcon sx={{ ml: 1, fontSize: 18, color: 'text.secondary' }} />
              </Tooltip>
            </Box>
            
            <Tooltip title={
              isLoading ? "Đang xử lý..." : 
              (!manualContent && signMethod === 'manual') ? "Vui lòng nhập nội dung để ký" :
              (!fileToSign && signMethod === 'file') ? "Vui lòng chọn file để ký" :
              !selectedSignId ? "Vui lòng chọn chữ ký" :
              "Ký tài liệu với chữ ký đã chọn"
            }>
              <span>
          <Button
            variant="contained"
            color="primary"
            fullWidth
                  size="large"
            onClick={signMethod === 'manual' ? handleSignManualText : handleSignFileWithMethod}
            disabled={isLoading || (!manualContent && signMethod === 'manual') || (!fileToSign && signMethod === 'file') || !selectedSignId}
                  sx={{ py: 1.5, fontWeight: 'bold', borderRadius: 2 }}
                  startIcon={isLoading ? <CircularProgress size={24} color="inherit" /> : <CreateIcon />}
          >
                  {isLoading ? 'Đang xử lý...' : 'Ký văn bản'}
          </Button>
              </span>
            </Tooltip>
          </Paper>
        </Paper>
        
        {signedFileUrl && (
          <Paper sx={{ p: 3, borderRadius: 2, boxShadow: 3, bgcolor: '#f0f8ff', mt: 4 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h5" sx={{ fontWeight: 'bold', display: 'flex', alignItems: 'center' }}>
                <VerifiedUserIcon sx={{ mr: 1, color: 'success.main' }} />
                Kết quả chữ ký nhúng
              </Typography>
            </Box>
            
            <Divider sx={{ mb: 2 }} />
            
            <Alert severity="success" sx={{ mb: 3 }} variant="filled">
              <AlertTitle>Ký số thành công!</AlertTitle>
              Tài liệu đã được ký số nhúng thành công và đã được tải xuống tự động.
            </Alert>
            
            <Box sx={{ mt: 2, display: 'flex', justifyContent: 'flex-end', gap: 2 }}>
              <Button
                variant="contained"
                color="primary"
                onClick={() => {
                  const link = document.createElement('a');
                  link.href = signedFileUrl;
                  link.download = `signed_${fileName || 'document'}.pdf`;
                  document.body.appendChild(link);
                  link.click();
                  document.body.removeChild(link);
                }}
                startIcon={<DownloadIcon />}
                sx={{ fontWeight: 'medium' }}
              >
                Tải lại file đã ký
              </Button>
            </Box>
          </Paper>
        )}
        
        {signature && (
          <Paper sx={{ p: 3, borderRadius: 2, boxShadow: 3, bgcolor: '#f0f8ff', mt: 4 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h5" sx={{ fontWeight: 'bold', display: 'flex', alignItems: 'center' }}>
                <VerifiedUserIcon sx={{ mr: 1, color: 'success.main' }} />
                Kết quả chữ ký
              </Typography>
              <Box>
                <Tooltip title="Sao chép chữ ký">
                  <IconButton onClick={copyToClipboard} size="small" sx={{ color: 'primary.main' }}>
                  <ContentCopyIcon />
                </IconButton>
                </Tooltip>
              </Box>
            </Box>
            
            <Divider sx={{ mb: 2 }} />
            
            <Alert severity="success" sx={{ mb: 3 }} variant="filled">
              <AlertTitle>Ký số thành công!</AlertTitle>
              Tài liệu đã được ký số thành công. Bạn có thể sao chép chữ ký hoặc tải xuống để sử dụng.
            </Alert>
            
            <Paper variant="outlined" sx={{ p: 2, bgcolor: '#fff', mb: 3, borderRadius: 1 }}>
              <Typography variant="subtitle2" color="textSecondary" gutterBottom sx={{ display: 'flex', alignItems: 'center' }}>
                <KeyIcon sx={{ mr: 1, fontSize: 18, color: 'primary.main' }} />
                Chữ ký số (base64):
              </Typography>
            <TextField
              multiline
              rows={4}
              fullWidth
              variant="outlined"
              value={signature}
              InputProps={{
                readOnly: true,
                  sx: { fontFamily: 'monospace', fontSize: '0.875rem' }
              }}
            />
            </Paper>
            
            <Box sx={{ bgcolor: '#f5f5f5', p: 2, borderRadius: 1, mb: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 'bold', mb: 1 }}>
                Thông tin chữ ký
              </Typography>
              
              <Box sx={{ display: 'flex', flexDirection: { xs: 'column', sm: 'row' }, gap: 2 }}>
                <Paper variant="outlined" sx={{ p: 1.5, flex: 1, bgcolor: '#fff' }}>
                  <Typography variant="body2" color="textSecondary">
                    Thuật toán băm
                  </Typography>
                  <Typography variant="body1" sx={{ fontWeight: 'medium', display: 'flex', alignItems: 'center' }}>
                    <SettingsIcon sx={{ mr: 0.5, fontSize: 16, color: 'primary.main' }} />
                    {hashAlgorithm}
                  </Typography>
                </Paper>
                
                <Paper variant="outlined" sx={{ p: 1.5, flex: 1, bgcolor: '#fff' }}>
                  <Typography variant="body2" color="textSecondary">
                    Thời gian ký
                  </Typography>
                  <Typography variant="body1" sx={{ fontWeight: 'medium' }}>
                    {new Date().toLocaleString()}
                  </Typography>
                </Paper>
                
                <Paper variant="outlined" sx={{ p: 1.5, flex: 1, bgcolor: '#fff' }}>
                  <Typography variant="body2" color="textSecondary">
                    Tên tài liệu
                  </Typography>
                  <Typography variant="body1" sx={{ fontWeight: 'medium' }}>
                    {fileName || "Văn bản thủ công"}
                  </Typography>
                </Paper>
              </Box>
            </Box>
            
            <Box sx={{ mt: 2, display: 'flex', justifyContent: 'flex-end', gap: 2 }}>
              <Button
                variant="contained"
                color="primary"
                onClick={exportSignatureAsFile}
                startIcon={<DownloadIcon />}
                sx={{ fontWeight: 'medium' }}
              >
                Xuất chữ ký
              </Button>
              <Button
                variant="outlined"
                color="secondary"
                onClick={exportPublicKeyAsFile}
                startIcon={<KeyIcon />}
                sx={{ fontWeight: 'medium' }}
              >
                Xuất khóa công khai
              </Button>
              {signMethod === 'manual' && manualContent && (
                <Button
                  variant="outlined"
                  color="info"
                  onClick={downloadManualTextAsFile}
                  startIcon={<DescriptionIcon />}
                  sx={{ fontWeight: 'medium' }}
                >
                  Tải văn bản TXT
                </Button>
              )}
            </Box>
          </Paper>
        )}
      </Container>
    </Box>
  );
};

export default Sign_file;
