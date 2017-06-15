using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Server
{
    public static class ByteImageConverter
    {
        public static ImageSource ByteToImage(byte[] imageData)
        {
            BitmapImage biImg = new BitmapImage();
            MemoryStream ms = new MemoryStream(imageData);
            biImg.BeginInit();
            biImg.StreamSource = ms;
            biImg.EndInit();
            return biImg;
        }

        public static string ImageToByte(FileStream fs)
        {
            byte[] imgBytes = new byte[fs.Length];
            fs.Read(imgBytes, 0, Convert.ToInt32(fs.Length));
            string encodeData = Convert.ToBase64String(imgBytes, Base64FormattingOptions.InsertLineBreaks);
            return encodeData;
        }
    }
}
