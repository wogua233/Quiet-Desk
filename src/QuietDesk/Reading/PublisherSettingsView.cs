using System;
using System.Windows;
using System.Windows.Controls;

namespace QuietDesk.Reading;

internal sealed partial class ReadingView
{
    private void AddPublisherSettings(Panel parent)
    {
        var form=Stack();
        form.Children.Add(Ui.Text("来源 API 与翻译 AI 是不同服务。保存后，对应刊物优先使用正式 API；未配置时使用官方 RSS。不会申请账号、读取浏览器 Cookie 或自动产生翻译请求。",12));
        void KeyField(string label,string provider,Func<string> read,Action<string> save,string link)
        {
            Ui.Gap(form,12);form.Children.Add(Ui.Text(label,14,"#FCFCFC"));
            var note=Ui.Text(read().Length>0?"已配置 · 密钥留空保留":"未配置 · 当前使用官方 RSS",12);form.Children.Add(note);
            var password=new PasswordBox{MinHeight=40,FontSize=14,Foreground=Ui.B("#FCFCFC"),Template=InputTemplate(typeof(PasswordBox))};
            System.Windows.Automation.AutomationProperties.SetName(password,label+"密钥");form.Children.Add(password);
            var actions=new WrapPanel{Margin=new Thickness(0,8,0,0)};
            actions.Children.Add(ActionButton("保存来源密钥",()=>{
                if(password.Password.Trim().Length==0){status.Text="未填写新密钥，保留原配置。";return;}
                save(password.Password.Trim());password.Clear();model.Service!.Configure(model.Settings);note.Text=provider=="APS"?"已配置 · API 用于补充摘要，目录继续使用 RSS":"已配置 · 对应来源将优先使用 API";status.Text=provider=="APS"?"APS 密钥已加密保存；重新打开摘要不足的文章即可补充。":provider+" 密钥已加密保存；请在订阅页重新检查对应刊物。";
            }));
            actions.Children.Add(ActionButton("移除来源密钥",()=>{save("");password.Clear();model.Service!.Configure(model.Settings);note.Text="未配置 · 当前使用官方 RSS";status.Text=provider=="APS"?"APS 密钥已移除，摘要 API 仅请求公开内容。":provider+" 密钥已移除，对应来源恢复官方 RSS。";}));
            actions.Children.Add(ActionButton("申请／查看接口",()=>Open(link)));form.Children.Add(actions);
        }
        KeyField("Springer Nature Meta API（Nature、Nature Communications、Nature Chemistry）","Springer Nature",()=>model.Settings.SpringerKey,model.Settings.SetSpringerKey,"https://dev.springernature.com/");
        KeyField("Guardian Content API（World）","Guardian",()=>model.Settings.GuardianKey,model.Settings.SetGuardianKey,"https://open-platform.theguardian.com/access/");
        KeyField("APS Harvest API（PRL、PRX、PRB、PRE 摘要补充）","APS",()=>model.Settings.ApsKey,model.Settings.SetApsKey,"https://harvest.aps.org/");
        form.Children.Add(Ui.Text("APS 保留官方 RSS 获取订阅目录；缺失摘要优先通过 Harvest API 获取。未配置 APS 密钥时仅取得接口允许公开访问的内容，不把开放文章目录当成全部文章。",12));
        Ui.Gap(form,8);form.Children.Add(Ui.Text("需自行申请服务密钥并按开通范围使用。每个服务在本应用最多 200 请求／日、单次最多 500 条；摘要和历史覆盖取决于服务。API 授权失败会显示原因，不悄悄退回网页。",12));
        var section=new Expander{Header="出版社／新闻来源 API（独立密钥）",Content=form,Foreground=Ui.B("#FCFCFC"),Margin=new Thickness(0,14,0,10)};parent.Children.Add(section);
    }
}
