using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiRomWeb.Core.Helper
{
    public static class MaskHelper
    {
        /// <summary>
        /// 对字符串进行掩码（中间替换为掩码字符）
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="visibleStart">开头可见字符数</param>
        /// <param name="visibleEnd">结尾可见字符数</param>
        /// <param name="maskChar">掩码字符，默认为 '*'</param>
        /// <returns>掩码后的字符串</returns>
        public static string MaskString(string? input, int visibleStart = 3, int visibleEnd = 4, char maskChar = '*')
        {
            if (string.IsNullOrEmpty(input))
                return input;

            if (visibleStart < 0 || visibleEnd < 0)
                throw new ArgumentException("Visible lengths must be non-negative.");

            int totalVisible = visibleStart + visibleEnd;
            if (totalVisible >= input.Length)
                return new string(maskChar, input.Length); // 全部掩码

            string start = input.Substring(0, visibleStart);
            string end = input.Substring(input.Length - visibleEnd);
            int maskLength = input.Length - totalVisible;
            string masked = new string(maskChar, maskLength);

            return start + masked + end;
        }

        // 快捷方法：手机号掩码（138****1234）
        public static string MaskPhoneNumber(string? phone) =>
            MaskString(phone, 3, 4, '*');

        // 快捷方法：银行卡掩码（尾号4位可见）
        public static string MaskBankCard(string? card) =>
            MaskString(card, 0, 4, '*');

        // 快捷方法：邮箱掩码（j***@example.com）
        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
                return email;

            var parts = email.Split('@');
            if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]))
                return email;

            string local = parts[0];
            string maskedLocal = local.Length > 1
                ? local[0] + new string('*', local.Length - 1)
                : "*";

            return maskedLocal + "@" + parts[1];
        }
    }
}
