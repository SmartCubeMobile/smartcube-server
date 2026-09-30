// Copyright © 2010-2027 Principal Research Corporation Ltd. All rights reserved.
// No part of this software may be reproduced, stored in a retrieval system, or transmitted
// in any form or by any means, electronic, mechanical, photocopying, recording, or otherwise,
// without the prior written permission of Principal Research Corporation Ltd.

using Newtonsoft.Json;

namespace SmartCubeMobile
{
    public static class SmartJsonV2017
    {
        /// <summary>
        /// JSON Serialization
        /// </summary>
        internal static string JsonSerializer<T>(T value)
        {
            return JsonConvert.SerializeObject(value);
        }

        /// <summary>
        /// JSON Deserialization
        /// </summary>
        internal static T JsonDeserializer<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}