using System;
using System.Collections.Generic;
using System.Text;

namespace WhatsAppLib.Models
{
    public enum WriteBinaryType
    {
        DebugLog = 1,
        QueryResume,
        QueryReceipt,
        QueryMedia,
        QueryChat,
        QueryContacts,
        QueryMessages,
        Presence,
        PresenceSubscribe,
        Group,
        Read,
        Chat,
        Received,
        Pic,
        Status,
        Message,
        QueryActions,
        Block,
        QueryGroup,
        QueryPreview,
        QueryEmoji,
        QueryMessageInfo,
        Spam,
        QuerySearch,
        QueryIdentity,
        QueryUrl,
        Profile,
        Contact,
        QueryVcard,
        QueryStatus,
        QueryStatusUpdate,
        PrivacyStatus,
        QueryLiveLocations,
        LiveLocation,
        QueryVname,
        QueryLabels,
        Call,
        QueryCall,
        QueryQuickReplies
    }
}
