import React from 'react';
import { Link as RouterLink } from 'react-router-dom';
import {
  AppBar, Toolbar, IconButton, Typography, Container,
  Box, Paper, Button
} from '@mui/material';
import Grid from '@mui/material/GridLegacy';
import MenuIcon from '@mui/icons-material/Menu';
import LinkIcon from '@mui/icons-material/Link';
import DescriptionIcon from '@mui/icons-material/Description';
import VerifiedUserIcon from '@mui/icons-material/VerifiedUser';

const HomePage: React.FC = () => (
  <>
    <AppBar position="static">
      <Toolbar>
        <LinkIcon sx={{ mr: 1 }} />
        <Typography variant="h6" sx={{ flexGrow: 1, cursor: 'pointer', fontWeight: 'bold' }}>
          RSA DIGITAL SIGNATURE
        </Typography>
        <IconButton edge="end" color="inherit">
          <MenuIcon />
        </IconButton>
      </Toolbar>
    </AppBar>

    <Container sx={{ py: 4 }}>
      <Box textAlign="center" mb={4}>
        <Typography variant="h4" gutterBottom>
          Hệ thống Quản lý Chữ ký Số
        </Typography>
        <Typography variant="subtitle1" color="text.secondary">
          Quản lý, ký và xác thực tài liệu một cách an toàn
        </Typography>
      </Box>

      <Grid container spacing={3}>
        {[
          {
            icon: <LinkIcon fontSize="large" color="primary" />,
            title: 'Quản lý khóa',
            desc: 'Tạo, chỉnh sửa và quản lý các khóa RSA của bạn',
            color: 'primary' as const, link: '/signatures'
          },
          {
            icon: <DescriptionIcon fontSize="large" color="success" />,
            title: 'Ký văn bản',
            desc: 'Ký tài liệu với chữ ký số của bạn',
            color: 'success' as const, link: '/sign_file'
          },
          {
            icon: <VerifiedUserIcon fontSize="large" color="secondary" />,
            title: 'Xác thực',
            desc: 'Xác thực tính hợp lệ của chữ ký số',
            color: 'secondary' as const, link: '/verify_file'
          }
        ].map((card, idx) => (
          <Grid item xs={12} md={4} key={idx}>
            <Paper sx={{ p: 3, display: 'flex', flexDirection: 'column', gap: 2, borderLeft: theme => `4px solid ${theme.palette[card.color].main}` }}>
              <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
                {card.icon}
                <Typography variant="h6" sx={{ ml: 1 }}>
                  {card.title}
                </Typography>
              </Box>
              <Typography variant="body2" paragraph>
                {card.desc}
              </Typography>
              <Button component={RouterLink} to={card.link} variant="contained" color={card.color}>
                Đi đến
              </Button>
            </Paper>
          </Grid>
        ))}
      </Grid>

      {/* <Box mt={6}>
        <Typography variant="h5" gutterBottom>
          Thống kê
        </Typography>
        <Grid container spacing={4} justifyContent="center">
          {[
            { number: 2, label: 'Chữ ký đã tạo', color: 'primary' },
            { number: 15, label: 'Văn bản đã ký', color: 'success.main' },
            { number: 8, label: 'Xác thực thành công', color: 'secondary.main' }
          ].map((stat, i) => (
            <Grid item xs={12} md={4} key={i}>
              <Paper sx={{ p: 3, textAlign: 'center' }}>
                <Typography variant="h4" color={stat.color}>
                  {stat.number}
                </Typography>
                <Typography variant="subtitle1">
                  {stat.label}
                </Typography>
              </Paper>
            </Grid>
          ))}
        </Grid>
      </Box> */}
    </Container>
  </>
);

export default HomePage;