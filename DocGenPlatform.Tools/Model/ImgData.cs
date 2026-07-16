using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocGenPlatform.Tools.Model
{
    public class ImgData
    {
        ///<summary>
        ///
        ///</summary>
        public string original_name { get; set; }
        ///<summary>
        ///
        ///</summary>
        public string saved_name { get; set; }
        ///<summary>
        ///
        ///</summary>
        public string url { get; set; }
        ///<summary>
        ///
        ///</summary>
        public int size { get; set; }
    }

    public class ImgRoot
    {
        ///<summary>
        ///
        ///</summary>
        public bool success { get; set; }
        ///<summary>
        ///上传成功
        ///</summary>
        public string message { get; set; }
        ///<summary>
        ///
        ///</summary>
        public ImgData data { get; set; }
    }
}
