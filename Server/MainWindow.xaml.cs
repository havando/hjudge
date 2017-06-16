using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace Server
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var a = new MemoryStream();
            var b = Convert.FromBase64String(
                "AAABAAEAMCoAAAEAIAD4IAAAFgAAACgAAAAwAAAAVAAAAAEAIAAAAAAAgB8AAAAAAAAAAAAAAAAAAAAAAAA+LRIAhXtuAP///T359PLq+fTy/fn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08vz59PL8+fTy/Pn08uz//vxChXxvAD4tEgA/LRIlIhQAP7KurI359PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v+3s7CRIRQAPz8tEic+LRLWLR8K+GdkYfr59PL/+fTy//n08v/49PL/+PTx//n08v/59PL/+PTx//n08v/59PL/+PTx//j08v/59PL/+PTx//j08v/59PL/+PTx//j08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//r18/9raGb5LB4K+D4tEts+LRL7LR8K/2ViX/759PL/+fTy//v08//I6tj/leC+/+zx6//Y7uH/kd+8/+Hv5v/l8Oj/kuC8/9Xt3//u8uz/l+G//8Xq1//18/D/oePE/7bnz//59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//r18/9pZmT+LB4K/z4tEv8+LRL5LR8K/2ViX/759PL/+fTy//v08//B6dX/iN63/+vx6//U7d//g920/9/v5P/j8Ob/hd21/9Ds3f/u8uz/it64/7/p1P/18/D/leC+/63lyv/69PP/+fTy//n08v/59PL/+fTy//r08v/69PP/+vTz//r08//69PP/+vTz//r08//69PP/+vTy//n08v/59PL/+fTy//n08v/59PL/+vTy//n08v/59PL/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL5LR8K/2ViX/759PL/+fTy//n08v/39PH/8PLt/63lyv/A6dT/9PPw/7jn0P+05s7/9PPv/8Pp1v+r5cn/8fLu/9Ds3f+k48X/6/Hr/9zu4/+g4sP/5PDn//r08//69PL/7PHr/6Pjxf+W4b7/l+G//5fhv/+X4b//l+G//5fhv/+W4L7/qOTI//Hy7v/59PL/+fTy//r08v/u8uz/o+PF/8vr2v/69PP/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL5LR8K/2ViX/759PL/+fTy//n08v/69PP/9fPw/5Pgvf+v5cv/+/Tz/6Ljxf+d4sL/+vTz/7Pmzf+P37v/9fPw/8bq1/+F3bX/7fLs/9ft4P+A3LP/3e7j//v08//69PP/5/Dp/4Tdtf9z2az/dNqs/3TarP902qz/dNqs/3TarP9y2az/i965/+/y7f/69PL/+fTy//r08//q8er/hd21/7ro0f/79PP/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL5LR8K/2ViX/759PL/+fTy//r08//R7N3/p+TH/+jx6f/a7uL/pOPF/+Dv5f/j8Of/pePG/9ft4P/q8er/qeTI/8zr2v/v8u3/sObM/8Dp1P/y8+7/9/Tx//n08v/59PL/+PTx//Lz7v/x8u7/8fLu//Hy7v/x8u7/8fLu//Hy7v/x8u7/8vPv//j08v/59PL/+fTy//n08v/49PL/8vPu//Xz8P/59PL/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL7LR8K/2RhX//59PL/+fTy//v08/+86NL/ftyy/+nx6v/R7N3/eduv/9zu4//h7+X/etuw/83r2//s8ev/gdyz/7rn0f/18/D/jN65/6bkx//69PL/+fTy//n08v/59PL/+fTy//n08v/69PL/+fTy//n08v/59PL/+fTy//n08v/69PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//r18/9pZWP/LB8K/z4tEv8+LRKfLB4JznJu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1//59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//r18/9pZmP/LB4K/z4tEv8+LRL5LR8K/2ViX/759PL/+fTy//v08/+459D/dtqt/+rx6v/P7Nz/cdmr/9vu4//g7+X/ctmr/8vr2v/t8uz/eduv/7bnz//28/H/hd21/6HjxP/89fP/+vTy//n08v/59PL/+fTy//v08//89fP//PXz//z18//89fP//PXz//z18//89fP/+/Tz//n08v/59PL/+fTy//n08v/59PL/+/Tz//r08//59PL/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL5LR8K/2ViX/759PL/+fTy//n08v/y8+7/5vDo/7zo0v/I6tn/6fHq/8Pp1v/A6dT/6fHq/8rr2v+66NH/5/Dp/9Ps3v+1587/4/Dn/9ru4v+z5s3/6fHq//r08//69PL/7/Lt/7Xnzv+r5cn/rOXK/6zlyv+s5cr/rOXK/6zlyv+r5cn/uefR//Pz7//59PL/+fTy//r08v/w8u3/tefP/9Tt3//69PP/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL5LR8K/2ViX/759PL/+fTy//n08v/79PP/9vPx/4Xdtf+l48b//fX0/5bhvv+R37v//PXz/6rkyf+B3LP/9vPx/7/p1P912q3/7PHr/9Pt3v9v2ar/2e7h//v08//69PP/5fDn/3ParP9g1qL/Ydaj/2HWo/9h1qP/Ydaj/2HWo/9g1qL/fNux/+3y7P/69PL/+fTy//r08//n8en/dNqt/7HmzP/79PP/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL5LR8K/2ViX/759PL/+fTy//r08//d7uP/v+nU/+Hv5f/a7uL/vujT/93v5P/f7+T/vujT/9nu4f/i7+b/wenU/9Pt3v/k8Of/xOrW/83r2//m8Oj/9PPw//n08v/59PL/9vPw/+bw6P/j8Of/4/Dn/+Pw5//j8Of/4/Dn/+Pw5//j8Of/5/Dp//f08f/59PL/+fTy//n08v/39PH/5vDo/+/y7f/59PL/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL7LR8K/2ViX//59PL/+fTy//v08/+258//cdmr/+jx6f/N69v/bNio/9ru4v/f7+T/bdip/8jq2f/s8ev/dNqt/7Tmzv/18/D/gdyz/57iwv/79PP/+fTy//n08v/59PL/+fTy//r08//69PP/+vTz//r08//69PP/+vTz//r08//69PP/+vTz//n08v/59PL/+fTy//n08v/59PL/+vTz//r08v/59PL/+fTy//r18/9pZmP/LB4K/z4tEv8+LRK+LR8K6GtoZfD59PL/+fTy//n08v/t8uz/4u/m//bz8P/x8u7/4e/l//Pz7//08/D/4e/l//Dy7v/39PH/4u/m/+3y7P/49PL/5PDn/+nx6v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//r18/9vbGrxKx4K6D4tEsM/LRMTFwgAI8rFw3v59PL/+/b0//349v/9+Pb//vj2//349v/9+Pb//vj2//349v/9+Pb//vj2//349v/9+Pb//vj2//349v/9+Pb//vj2//749v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb/+/b0//n08v/NycaAFggAIz8tExQ/LRMAj4Z6AP/9+yn69fOw08/N5bq3tf+7uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv67uLb+u7i2/ru4tv66t7X/0s7M5fr187L//PotkYd8AD8tEwAAAAAA+fTyAPfy8AD///8ELCQYkiQcEf8kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH+JBwR/iQcEf4kHBH/KyMYl////wX18O4A+fTyAAAAAAAAAAAA+fTyAL20qwBbTDUAPiwRjD4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/Pi0S/z4tEv8+LRL/PiwRkmRVQACnnI8A+fTyAAAAAAAAAAAA+fTyAPjz8QB6cmgAJBoKjSQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoK/yQaCv8kGgr/JBoKkoZ+dAD38vAA+fTyAAAAAAA/LhMArKSbAP759xz79vSJuLSy1JaTkv+XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6XlJP+l5ST/peUk/6Wk5L/trKx1vr184v++fcfrqadAD8uEwBALhMFAAAADOLd22n59PL/+/b0//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb//fj2//349v/9+Pb/+/b0//n08v/k391vAAAADEAuEwY+LRKeLB4JzHJvbOH59PL/+fTy//n08v/69PP/+/Tz//n08v/69PL//PXz//r08v/59PL//PTz//r08v/59PL/+/Tz//r08//59PL/+/Tz//v08//59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//r18/93dHHiKx4JzD4tEqI+LRL7LR8K/2RhX//59PL/+fTy//r08v/g7+X/x+rY//Lz7//p8en/xerX/+3y7P/v8u3/xurX/+fw6f/08+//yOrZ/9/v5f/39PH/zevb/9ft4P/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//r18/9pZWP/LB8K/z4tEv8+LRL5LR8K/2ViX/759PL/+fTy//v08/+1587/b9mq/+nx6v/N69v/atin/9ru4v/f7+X/a9io/8jq2f/t8uv/c9ms/7Pmzf/28/D/f9yy/53iwv/89fT/+vTy//n08v/59PL/+fTy//v08//89fP//PXz//z18//89fP//PXz//z18//89fP/+/Tz//n08v/59PL/+fTy//n08v/59PL/+/Tz//r08//59PL/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL5LR8K/2ViX/759PL/+fTy//r08v/r8ev/2+7i/8/s3P/T7d7/3O7j/9Hs3f/R7N3/3O7j/9Tt3//O7Nz/3O7j/9ft4P/M69v/2+7i/9nu4f/M69r/7vLt//r08v/59PL/8vPu/83r2//H6tj/x+rY/8fq2P/H6tj/x+rY/8fq2P/G6tj/0Ozc//Xz8P/59PL/+fTy//n08v/z8+//zevb/+Hv5v/69PL/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL5LR8K/2ViX/759PL/+fTy//n08v/79PP/9vPx/3/csv+g4sT//fX0/5HgvP+L3rn//PX0/6bkxv9727D/9vPx/7zo0v9u2an/7PHr/9Hs3f9o16b/2O3h//v08//69PP/5PDn/2zYqP9Y1J7/WdSf/1nUn/9Z1J//WdSf/1nUn/9Y1J7/ddqt/+zy6//69PL/+fTy//r08//n8Oj/bdip/63lyv/79PP/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL5LR8K/2ViX/759PL/+fTy//r08v/m8Oj/0Ozd/9Tt3v/V7d//0ezd/9Tt3//U7d//0ezd/9Xt3//T7d7/0ezd/9Tt3//T7N7/0uze/9Tt3v/T7N7/8PLt//r08v/59PL/8/Pv/9Tt3//P7Nz/z+zc/8/s3P/P7Nz/z+zc/8/s3P/P7Nz/1u3g//bz8P/59PL/+fTy//n08v/08+//1O3f/+Xw6P/69PL/+fTy//r18/9pZmT+LB4K/z4tEv0+LRL7LR8K/2ViX/759PL/+fTy//z18/+z5s3/a9io/+jx6f/M69r/Zdel/9nu4f/e7+T/Z9em/8fq2P/s8ev/btmp/7HmzP/28/D/fNuw/5rhwP/89fP/+vTy//n08v/59PL/+fTy//v08//79PP/+/Tz//v08//79PP/+/Tz//v08//79PP/+/Tz//n08v/59PL/+fTy//n08v/59PL/+/Tz//r08//59PL/+fTy//r18/9pZmT+LB4K/z4tEv8+LRLXLR8K+WdjYfr59PL/+fTy//r08v/j8Ob/zOvb//Pz7//q8er/y+va/+7y7P/w8u3/y+va/+nx6f/08/D/zevb/+Lv5v/39PH/0uzd/9vu4v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//r18/9raGX6LB4K+T4tEtw/LRInIxUAQrCsqo/59PL/+fTy//n08v/69PP/+/Tz//n08v/69PL/+/Tz//r08v/59PL/+/Tz//r08v/59PL/+/Tz//r08//59PL/+/Tz//v08//59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v/59PL/+fTy//n08v+1sa6TIhQAQj8tEik+LRIAhHptAP///T759PLs+fTy/vn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/fn08v359PL9+fTy/vn08u7//vxDhXtvAD4tEgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAEAAIAAAAAAAQAAgAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAEAAIAAAAAAAQAAgAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
            a.Write(b, 0, b.Length);
            var open = new MenuItem("还原");
            open.Click += (sender, args) =>
            {
                Visibility = Visibility.Visible;
                ShowInTaskbar = true;
                Activate();
            };
            MenuItem[] childen = { open };
            var notifyIcon = new NotifyIcon
            {
                BalloonTipText = @"欢迎使用 hjudge，程序将在后台保持运行",
                Text = @"hjudge - server",
                Icon = new Icon(a),
                ContextMenu = new ContextMenu(childen)
            };
            notifyIcon.ShowBalloonTip(2000);
            notifyIcon.MouseClick += (sender, args) =>
            {
                if (args.Button != MouseButtons.Left) return;
                Visibility = Visibility.Visible;
                ShowInTaskbar = true;
                Activate();
            };
            Init();
        }

        private void Init()
        {
            Height = 228; Width = 473;
            LoginGrid.Margin = new Thickness(10, 10, 0, 0);
            ContentGrid.Margin = new Thickness(10, 10, 0, 0);
            ContentGrid.Opacity = 0;
            LoginGrid.Visibility = Visibility.Visible;
            ContentGrid.Visibility = Visibility.Hidden;
            if (!Directory.Exists(Environment.CurrentDirectory + "\\AppData"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "\\AppData");
            }
            if (!Directory.Exists(Environment.CurrentDirectory + "\\Problems"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "\\Problems");
            }
            if (!Directory.Exists(Environment.CurrentDirectory + "\\Data"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "\\Data");
            }
            Connection.Init();
            Configuration.Init();
            CurrentAddress.Content = "当前主机地址：" + Connection.Address;
            UserHelper.SetCurrentUser(0, "", "", "", 0, "", "");
            UserHelper.CurrentUser.IsChanged = false;
            ShowUserInfo();
        }

        private async void LoginButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            LoginButton.IsEnabled = false;
            var res = await Connection.Login(UserName.Text, Password.Password);
            switch (res)
            {
                case 1:
                    {
                        MessageBox.Show("用户名或密码错误", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    }
                default:
                    {
                        if (res != 0)
                        {
                            MessageBox.Show("未知错误", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        break;
                    }
            }
            LoginButton.IsEnabled = true;
            if (res == 0)
            {
                UserName.Text = "";
                Password.Password = "";
                LoginGrid.Visibility = Visibility.Hidden;
                ContentGrid.Visibility = Visibility.Visible;
                var hiddenDaV = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5)));
                var showDaV = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5)));
                var scratchWidthDaV = new DoubleAnimation(473, 673, new Duration(TimeSpan.FromSeconds(0.25)));
                var scratchHeightDaV = new DoubleAnimation(228, 328, new Duration(TimeSpan.FromSeconds(0.25)));
                LoginGrid.BeginAnimation(OpacityProperty, hiddenDaV);
                BeginAnimation(WidthProperty, scratchWidthDaV);
                await Task.Run(() => { Thread.Sleep(250); });
                BeginAnimation(HeightProperty, scratchHeightDaV);
                ContentGrid.BeginAnimation(OpacityProperty, showDaV);
                switch (UserHelper.CurrentUser.Type)
                {
                    case 1:
                        SetEnvironmentForBoss();
                        break;
                    case 2:
                        SetEnvironmentForAdministrator();
                        break;
                    case 3:
                        SetEnvironmentForTeacher();
                        break;
                    case 4:
                        SetEnvironmentForStudent();
                        break;
                }
            }
        }

        private void SetEnvironmentForBoss()
        {
            Button[] operationsButton =
            {
                new Button {Height = 32, Width = 80, Content = "个人信息"},
                new Button {Height = 32, Width = 80, Content = "题目管理"},
                new Button {Height = 32, Width = 80, Content = "评测日志"},
                new Button {Height = 32, Width = 80, Content = "发送消息"},
                new Button {Height = 32, Width = 80, Content = "人员管理"},
                new Button {Height = 32, Width = 80, Content = "离线评测"},
                new Button {Height = 32, Width = 80, Content = "系统设置"},
                new Button {Height = 32, Width = 80, Content = "注销登录"},
                new Button {Height = 32, Width = 80, Content = "退出程序"}
            };
            operationsButton[0].Click += (o, args) =>
            {
                var a = new ProfilesManage();
                a.Show();
            };
            operationsButton[1].Click += (o, args) => { }; //TODO: Problems Management
            operationsButton[2].Click += (o, args) => {
                var a = new JudgeLogs();
                a.Show();
            };
            operationsButton[3].Click += (o, args) => { }; //TODO: Messaging
            operationsButton[4].Click += (o, args) =>
            {
                var a = new MembersManagement();
                a.Show();
            };
            operationsButton[5].Click += (o, args) => { }; //TODO: Offline Judge
            operationsButton[6].Click += (o, args) =>
            {
                var a = new SystemConfiguratioin();
                a.Show();
            };
            operationsButton[7].Click += async (o, args) =>
            {
                var hiddenDaV = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5)));
                var showDaV = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5)));
                var unscratchWidthDaV = new DoubleAnimation(673, 473, new Duration(TimeSpan.FromSeconds(0.25)));
                var unscratchHeightDaV = new DoubleAnimation(328, 228, new Duration(TimeSpan.FromSeconds(0.25)));
                await Dispatcher.BeginInvoke((Action)(() =>
                 {
                     LoginGrid.Visibility = Visibility.Visible;
                     ContentGrid.Visibility = Visibility.Hidden;
                     UserHelper.SetCurrentUser(0, "", "", "", 0, "", "");
                     UserHelper.CurrentUser.IsChanged = false;
                     Operations.Items.Clear();
                 }));
                ContentGrid.BeginAnimation(OpacityProperty, hiddenDaV);
                BeginAnimation(WidthProperty, unscratchWidthDaV);
                await Task.Run(() => { Thread.Sleep(250); });
                BeginAnimation(HeightProperty, unscratchHeightDaV);
                LoginGrid.BeginAnimation(OpacityProperty, showDaV);
            };
            operationsButton[8].Click += (o, args) =>
            {
                Environment.Exit(0);
            };
            foreach (var t in operationsButton)
            {
                Operations.Items.Add(t);
            }
        }

        private void SetEnvironmentForTeacher()
        {
            Button[] operationsButton =
            {
                new Button {Height = 32, Width = 80, Content = "个人信息"},
                new Button {Height = 32, Width = 80, Content = "题目管理"},
                new Button {Height = 32, Width = 80, Content = "评测日志"},
                new Button {Height = 32, Width = 80, Content = "发送消息"},
                new Button {Height = 32, Width = 80, Content = "选手管理"},
                new Button {Height = 32, Width = 80, Content = "离线评测"},
                new Button {Height = 32, Width = 80, Content = "注销登录"}
            };
            operationsButton[0].Click += (o, args) =>
            {
                var a = new ProfilesManage();
                a.Show();
            };
            operationsButton[1].Click += (o, args) => { }; //TODO: Problems Management
            operationsButton[2].Click += (o, args) =>
            {
                var a = new JudgeLogs();
                a.Show();
            };
            operationsButton[3].Click += (o, args) => { }; //TODO: Messaging
            operationsButton[4].Click += (o, args) =>
            {
                var a = new MembersManagement();
                a.Show();
            };
            operationsButton[5].Click += (o, args) => { }; //TODO: Offline Judge
            operationsButton[6].Click += async (o, args) =>
            {
                var hiddenDaV = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5)));
                var showDaV = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5)));
                var unscratchWidthDaV = new DoubleAnimation(673, 473, new Duration(TimeSpan.FromSeconds(0.25)));
                var unscratchHeightDaV = new DoubleAnimation(328, 228, new Duration(TimeSpan.FromSeconds(0.25)));
                await Dispatcher.BeginInvoke((Action)(() =>
                {
                    LoginGrid.Visibility = Visibility.Visible;
                    ContentGrid.Visibility = Visibility.Hidden;
                    UserHelper.SetCurrentUser(0, "", "", "", 0, "", "");
                    UserHelper.CurrentUser.IsChanged = false;
                    Operations.Items.Clear();
                }));
                ContentGrid.BeginAnimation(OpacityProperty, hiddenDaV);
                BeginAnimation(WidthProperty, unscratchWidthDaV);
                await Task.Run(() => { Thread.Sleep(250); });
                BeginAnimation(HeightProperty, unscratchHeightDaV);
                LoginGrid.BeginAnimation(OpacityProperty, showDaV);
            };
            foreach (var t in operationsButton)
            {
                Operations.Items.Add(t);
            }
        }

        private void SetEnvironmentForAdministrator()
        {
            Button[] operationsButton =
            {
                new Button {Height = 32, Width = 80, Content = "个人信息"},
                new Button {Height = 32, Width = 80, Content = "题目管理"},
                new Button {Height = 32, Width = 80, Content = "评测日志"},
                new Button {Height = 32, Width = 80, Content = "发送消息"},
                new Button {Height = 32, Width = 80, Content = "人员管理"},
                new Button {Height = 32, Width = 80, Content = "离线评测"},
                new Button {Height = 32, Width = 80, Content = "系统设置"},
                new Button {Height = 32, Width = 80, Content = "注销登录"},
                new Button {Height = 32, Width = 80, Content = "退出程序"}
            };
            operationsButton[0].Click += (o, args) =>
            {
                var a = new ProfilesManage();
                a.Show();
            };
            operationsButton[1].Click += (o, args) => { }; //TODO: Problems Management
            operationsButton[2].Click += (o, args) => {
                var a = new JudgeLogs();
                a.Show();
            };
            operationsButton[3].Click += (o, args) => { }; //TODO: Messaging
            operationsButton[4].Click += (o, args) =>
            {
                var a = new MembersManagement();
                a.Show();
            };
            operationsButton[5].Click += (o, args) => { }; //TODO: Offline Judge
            operationsButton[6].Click += (o, args) =>
            {
                var a = new SystemConfiguratioin();
                a.Show();
            }; 
            operationsButton[7].Click += async (o, args) =>
            {
                var hiddenDaV = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5)));
                var showDaV = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5)));
                var unscratchWidthDaV = new DoubleAnimation(673, 473, new Duration(TimeSpan.FromSeconds(0.25)));
                var unscratchHeightDaV = new DoubleAnimation(328, 228, new Duration(TimeSpan.FromSeconds(0.25)));
                await Dispatcher.BeginInvoke((Action)(() =>
                {
                    LoginGrid.Visibility = Visibility.Visible;
                    ContentGrid.Visibility = Visibility.Hidden;
                    UserHelper.SetCurrentUser(0, "", "", "", 0, "", "");
                    UserHelper.CurrentUser.IsChanged = false;
                    Operations.Items.Clear();
                }));
                ContentGrid.BeginAnimation(OpacityProperty, hiddenDaV);
                BeginAnimation(WidthProperty, unscratchWidthDaV);
                await Task.Run(() => { Thread.Sleep(250); });
                BeginAnimation(HeightProperty, unscratchHeightDaV);
                LoginGrid.BeginAnimation(OpacityProperty, showDaV);
            };
            operationsButton[8].Click += (o, args) =>
            {
                Environment.Exit(0);
            };
            foreach (var t in operationsButton)
            {
                Operations.Items.Add(t);
            }
        }

        private void SetEnvironmentForStudent()
        {
            Button[] operationsButton =
            {
                new Button {Height = 32, Width = 80, Content = "个人信息"},
                new Button {Height = 32, Width = 80, Content = "评测日志"},
                new Button {Height = 32, Width = 80, Content = "离线评测"},
                new Button {Height = 32, Width = 80, Content = "注销登录"}
            };
            operationsButton[0].Click += (o, args) =>
            {
                var a = new ProfilesManage();
                a.Show();
            };
            operationsButton[1].Click += (o, args) =>
            {
                var a = new JudgeLogs();
                a.Show();
            };
            operationsButton[2].Click += (o, args) => { }; //TODO: Offline Judge
            operationsButton[3].Click += async (o, args) =>
            {
                var hiddenDaV = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.5)));
                var showDaV = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.5)));
                var unscratchWidthDaV = new DoubleAnimation(673, 473, new Duration(TimeSpan.FromSeconds(0.25)));
                var unscratchHeightDaV = new DoubleAnimation(328, 228, new Duration(TimeSpan.FromSeconds(0.25)));
                await Dispatcher.BeginInvoke((Action)(() =>
                {
                    LoginGrid.Visibility = Visibility.Visible;
                    ContentGrid.Visibility = Visibility.Hidden;
                    UserHelper.SetCurrentUser(0, "", "", "", 0, "", "");
                    UserHelper.CurrentUser.IsChanged = false;
                    Operations.Items.Clear();
                }));
                ContentGrid.BeginAnimation(OpacityProperty, hiddenDaV);
                BeginAnimation(WidthProperty, unscratchWidthDaV);
                await Task.Run(() => { Thread.Sleep(250); });
                BeginAnimation(HeightProperty, unscratchHeightDaV);
                LoginGrid.BeginAnimation(OpacityProperty, showDaV);
            };
            foreach (var t in operationsButton)
            {
                Operations.Items.Add(t);
            }
        }

        private void ShowUserInfo()
        {
            Task.Run(() =>
            {
                while (!Environment.HasShutdownStarted)
                {
                    if (UserHelper.CurrentUser.IsChanged ?? false)
                    {
                        UserHelper.CurrentUser.IsChanged = false;
                        string idnty = null;
                        switch (UserHelper.CurrentUser.Type)
                        {
                            case 1:
                                idnty = "BOSS";
                                break;
                            case 2:
                                idnty = "管理员";
                                break;
                            case 3:
                                idnty = "教师";
                                break;
                            case 4:
                                idnty = "选手";
                                break;
                        }
                        Dispatcher.BeginInvoke((Action)(() => { Identity.Content = $"{UserHelper.CurrentUser.UserName}，欢迎回来！当前身份：{idnty}"; }));
                        if (!string.IsNullOrEmpty(UserHelper.CurrentUser.Icon))
                        {
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                UserIcon.Source =
                                ByteImageConverter.ByteToImage(Convert.FromBase64String(UserHelper.CurrentUser.Icon));
                            }));
                        }
                        else
                        {
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                UserIcon.Source = ByteImageConverter.ByteToImage(Convert.FromBase64String(
                                    "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V2WkckhEUgAAACV0RVh0ZGF0ZTpjcmVhdGUAMjAxNi0wOS0xN1QxNToyMjoxNSswODowMCsLl+sAAAAldEVYdGRhdGU6bW9kaWZ5ADIwMTYtMDUtMjZUMDA6NTk6MjErMDg6MDBsVmrEAAAATXRFWHRzb2Z0d2FyZQBJbWFnZU1hZ2ljayA3LjAuMS02IFExNiB4ODZfNjQgMjAxNi0wOS0xNyBodHRwOi8vd3d3LmltYWdlbWFnaWNrLm9yZ93ZpU4AAABjdEVYdHN2Zzpjb21tZW50ACBHZW5lcmF0b3I6IEFkb2JlIElsbHVzdHJhdG9yIDE5LjAuMCwgU1ZHIEV4cG9ydCBQbHVnLUluIC4gU1ZHIFZlcnNpb246IDYuMDAgQnVpbGQgMCkgIM5IkAsAAAAYdEVYdFRodW1iOjpEb2N1bWVudDo6UGFnZXMAMaf/uy8AAAAYdEVYdFRodW1iOjpJbWFnZTo6SGVpZ2h0ADUyOTNy2coAAAAXdEVYdFRodW1iOjpJbWFnZTo6V2lkdGgANTI5oIOJlwAAABl0RVh0VGh1bWI6Ok1pbWV0eXBlAGltYWdlL3BuZz+yVk4AAAAXdEVYdFRodW1iOjpNVGltZQAxNDY0MTk1NTYxyN1ubAAAABJ0RVh0VGh1bWI6OlNpemUAMjIuNktCSPcP1AAAAF90RVh0VGh1bWI6OlVSSQBmaWxlOi8vL2hvbWUvd3d3cm9vdC9zaXRlL3d3dy5lYXN5aWNvbi5uZXQvY2RuLWltZy5lYXN5aWNvbi5jbi9zcmMvMTIwMTQvMTIwMTQwOS5wbmdgVPHMAAAAAElFTkSuQmCC"));
                            }));
                        }
                    }
                    Thread.Sleep(1000);
                }
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            ShowInTaskbar = false;
            Visibility = Visibility.Hidden;
        }
    }
}
