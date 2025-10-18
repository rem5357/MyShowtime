# AWS Cognito Setup Guide for MyShowtime

This guide will walk you through setting up AWS Cognito User Pool for MyShowtime authentication.

## What is AWS Cognito?

**AWS Cognito** is Amazon's managed authentication service. Think of it as "user accounts as a service" - it handles:
- User registration and login
- Password management and resets
- Email verification
- Multi-factor authentication (MFA)
- OAuth2/OpenID Connect authentication

A **User Pool** is like a user directory - it stores your users' accounts (email, password, profile info).

---

## Step-by-Step Setup

### 1. Access AWS Cognito Console

1. Log into your AWS Console: https://console.aws.amazon.com/
2. In the search bar at the top, type "Cognito" and select **Amazon Cognito**
3. Make sure you're in the **US East (Ohio)** region (us-east-2) - check the region dropdown in the top-right corner

### 2. Create User Pool

1. Click **"Create user pool"** button
2. You'll go through a multi-step wizard...

---

### Step 1: Configure Sign-In Experience

**Sign-in options:**
- ✅ **Email** (users will sign in with their email address)

**User name requirements:**
- ✅ Make user name case sensitive

Click **Next**

---

### Step 2: Configure Security Requirements

**Password policy:**
- Select **Cognito defaults** (good balance of security)
- Minimum length: 8 characters
- Contains: lowercase, uppercase, numbers, special characters

**Multi-factor authentication (MFA):**
- Select **Optional MFA** (users can enable it if they want)
- MFA methods: ✅ **Authenticator apps** (like Google Authenticator)

**User account recovery:**
- ✅ **Enable self-service account recovery**
- Recovery method: ✅ **Email only**

Click **Next**

---

### Step 3: Configure Sign-Up Experience

**Self-service sign-up:**
- ✅ **Enable self-service sign-up** (your friends can create their own accounts)

**Attribute verification and user account confirmation:**
- Attributes to verify: ✅ **Send email message, verify email address**

**Required attributes:**
- ✅ **email** (required)
- ✅ **name** (required - will show in the app)

**Custom attributes:** (optional)
- Leave empty for now

Click **Next**

---

### Step 4: Configure Message Delivery

**Email:**
- Select **Send email with Cognito** (free tier, up to 50 emails/day)
- For production: You can configure Amazon SES later for higher limits

**SMS:** (optional)
- Skip this unless you want SMS-based MFA

**FROM email address:**
- Leave as `no-reply@verificationemail.com` (default)

**REPLY-TO email address:**
- Optional: Add your email if you want users to be able to reply

Click **Next**

---

### Step 5: Integrate Your App

**User pool name:**
- Enter: `MyShowtime-UserPool`

**Hosted authentication pages:**
- ✅ **Use the Cognito Hosted UI**

**Domain:**
- Domain type: Select **Use a Cognito domain**
- Cognito domain: Enter a unique prefix (e.g., `myshowtime-auth-<your-initials>`)
  - Example: `myshowtime-auth-rm`
  - This will create: `https://myshowtime-auth-rm.auth.us-east-2.amazoncognito.com`
  - Try different prefixes if your first choice is taken

**Initial app client:**
- App type: **Public client** (Blazor WASM can't keep secrets)
- App client name: `MyShowtime-Client`

**Client secret:**
- ✅ **Don't generate a client secret** (IMPORTANT!)

**Allowed callback URLs:**
```
https://goldshire.tail80a7ec.ts.net/MyShowtime/authentication/login-callback
```

**Allowed sign-out URLs:**
```
https://goldshire.tail80a7ec.ts.net/MyShowtime/
```

**Identity providers:**
- ✅ **Cognito user pool**
- (You can add Google/Facebook/etc. later if you want)

**OAuth 2.0 grant types:**
- ✅ **Authorization code grant**

**OpenID Connect scopes:**
- ✅ **OpenID**
- ✅ **Email**
- ✅ **Profile**

Click **Next**

---

### Step 6: Review and Create

1. Review all your settings
2. Click **Create user pool**
3. Wait for creation to complete (takes ~30 seconds)

---

## After Creation: Gather Configuration Values

Once your User Pool is created, you need to copy some values:

### 1. User Pool ID
- On the User Pool overview page, you'll see **User pool ID**
- Example: `us-east-2_aBcDeFgHi`
- **📋 Copy this value**

### 2. App Client ID
1. Click the **"App integration"** tab
2. Scroll down to **"App clients and analytics"**
3. Click your app client name (`MyShowtime-Client`)
4. You'll see **Client ID**
5. Example: `1a2b3c4d5e6f7g8h9i0j1k2l3m`
6. **📋 Copy this value**

### 3. Cognito Domain
1. Still in the **"App integration"** tab
2. Scroll to **"Domain"** section
3. You'll see your domain URL
4. Example: `https://myshowtime-auth-rm.auth.us-east-2.amazoncognito.com`
5. **📋 Copy this value**

### 4. AWS Region
- You already know this: `us-east-2`

---

## Provide These Values to Claude

Once you have all 4 values, provide them in this format:

```
User Pool ID: us-east-2_aBcDeFgHi
App Client ID: 1a2b3c4d5e6f7g8h9i0j1k2l3m
Cognito Domain: https://myshowtime-auth-rm.auth.us-east-2.amazoncognito.com
AWS Region: us-east-2
```

I'll use these values to configure the application!

---

## Testing Your User Pool (Optional)

You can test the sign-up flow now if you want:

1. Go to your Cognito Domain URL in a browser
2. Add this to the end: `/login?client_id=YOUR_CLIENT_ID&response_type=code&scope=email+openid+profile&redirect_uri=https://goldshire.tail80a7ec.ts.net/MyShowtime/authentication/login-callback`
3. Replace `YOUR_CLIENT_ID` with your actual Client ID
4. You should see a Cognito login page
5. Click "Sign up" to create a test account
6. Don't worry if the redirect fails - we haven't integrated it yet!

---

## Troubleshooting

**"Domain already exists"**
- Try a different domain prefix (add your initials or a number)

**"Invalid callback URL"**
- Make sure you copied the URL exactly: `https://goldshire.tail80a7ec.ts.net/MyShowtime/authentication/login-callback`
- No trailing slashes, no spaces

**Email not arriving**
- Check your spam folder
- Cognito free tier has sending limits
- Verify your email is correct in the user profile

---

## Next Steps

After you provide the 4 configuration values, I will:
1. Install authentication NuGet packages
2. Configure the Blazor client to use Cognito
3. Update the API to validate JWT tokens
4. Create user auto-provisioning logic
5. Test the complete authentication flow

Let me know when you have the values! 🚀
