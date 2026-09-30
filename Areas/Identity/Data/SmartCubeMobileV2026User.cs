// Copyright © 2010-2027 Principal Research Corporation Ltd. All rights reserved.
// No part of this software may be reproduced, stored in a retrieval system, or transmitted
// in any form or by any means, electronic, mechanical, photocopying, recording, or otherwise,
// without the prior written permission of Principal Research Corporation Ltd.

using Microsoft.AspNetCore.Identity;
using System;

namespace SmartCubeMobileV2026.Areas.Identity.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        public DateTime Expiration1 { get; set; }
        [PersonalData]
        public DateTime Expiration2 { get; set; }
        [PersonalData]
        public DateTime Expiration3 { get; set; }
        [PersonalData]
        public bool Subscriber { get; set; }
        [PersonalData]
        public bool Trace { get; set; }
        [PersonalData]
        public string ClearPassword { get; set; }
        [PersonalData]
        public DateTime LastLogOnTime { get; set; }
        [PersonalData]
        public bool MultipleMeter { get; set; }
        [PersonalData]
        public bool Administrator { get; set; }
    }
}