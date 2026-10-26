const authView = document.querySelector(".auth-layout");
const authForm = document.querySelector("#auth-form");
const authTabs = document.querySelector(".auth-tabs");
const forgotPasswordForm = document.querySelector("#forgot-password-form");
const resetPasswordForm = document.querySelector("#reset-password-form");
const appView = document.querySelector("#dating-app");
const authMessage = document.querySelector("#auth-message");
const authButton = document.querySelector("#auth-submit");
const appState = { token: localStorage.getItem("kiss.accessToken"), profile: null, interests: [], resetToken: null };
const chatState = {
  contacts: [],
  activeUserId: null,
  pendingUserId: null,
  pollTimer: null,
  isLoading: false,
  messages: [],
  lastMessageKey: null,
  mediaUrls: new Map()
};
let authMode = "login";

const escapeHtml = (value = "") => String(value).replace(/[&<>"']/g, (character) => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
}[character]));

function photoUrl(value) {
  return typeof value === "string" && (value.startsWith("/uploads/") || value.startsWith("/demo-profiles/")) ? value : "";
}

function cleanProfileName(value = "") {
  return String(value).replace(/^\[M\u1eabu\]\s*/u, "");
}

function ageFrom(value) {
  if (!value) return "";
  const birthDate = new Date(`${value}T00:00:00`);
  if (Number.isNaN(birthDate.getTime())) return "";
  const today = new Date();
  let age = today.getFullYear() - birthDate.getFullYear();
  if (today.getMonth() < birthDate.getMonth() || (today.getMonth() === birthDate.getMonth() && today.getDate() < birthDate.getDate())) age--;
  return age;
}

function setAuthMode(nextMode) {
  authMode = nextMode;
  const registering = authMode === "register";
  const fields = ["name-field", "gender-field", "birth-field", "terms-field"];
  authTabs.hidden = false;
  authForm.hidden = false;
  forgotPasswordForm.hidden = true;
  resetPasswordForm.hidden = true;
  document.querySelector("#forgot-password-link").hidden = registering;
  document.querySelector("#login-tab").classList.toggle("is-active", !registering);
  document.querySelector("#register-tab").classList.toggle("is-active", registering);
  document.querySelector("#login-tab").setAttribute("aria-selected", String(!registering));
  document.querySelector("#register-tab").setAttribute("aria-selected", String(registering));
  for (const id of fields) {
    const field = document.getElementById(id);
    field.hidden = !registering;
    const input = field.querySelector("input, select");
    input.disabled = !registering;
    input.required = registering;
  }
  document.querySelector("#privacy-note").hidden = !registering;
  authForm.elements.password.autocomplete = registering ? "new-password" : "current-password";
  document.querySelector("#auth-title").textContent = registering ? "Tạo tài khoản." : "Chào bạn.";
  document.querySelector("#auth-kicker").textContent = registering ? "BẮT ĐẦU MỘT CÂU CHUYỆN MỚI" : "RẤT VUI ĐƯỢC GẶP LẠI";
  document.querySelector("#auth-subtitle").textContent = registering
    ? "Tạo hồ sơ Kiss và mở đầu một cuộc gặp gỡ."
    : "Đăng nhập để tiếp tục hành trình gặp gỡ.";
  authButton.innerHTML = `${registering ? "Tạo tài khoản" : "Đăng nhập"} <span aria-hidden="true">↗</span>`;
  authMessage.hidden = true;
  authMessage.classList.remove("is-success");
}

async function request(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (appState.token) headers.set("Authorization", `Bearer ${appState.token}`);
  if (options.body && !(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
  const response = await fetch(`/api${path}`, { ...options, headers });
  if (response.status === 204) return null;
  const contentType = response.headers.get("content-type") || "";
  const result = contentType.includes("json") ? await response.json() : await response.text();
  if (!response.ok) throw new Error(result?.detail || result?.title || "Không thể hoàn tất yêu cầu.");
  return result;
}

function showForgotPassword() {
  authTabs.hidden = true;
  authForm.hidden = true;
  document.querySelector("#forgot-password-link").hidden = true;
  forgotPasswordForm.hidden = false;
  resetPasswordForm.hidden = true;
  document.querySelector("#auth-title").textContent = "Quên mật khẩu?";
  document.querySelector("#auth-kicker").textContent = "KHÔI PHỤC TÀI KHOẢN";
  document.querySelector("#auth-subtitle").textContent = "Nhập email đã đăng ký để nhận liên kết đặt lại mật khẩu.";
  document.querySelector("#forgot-password-message").hidden = true;
}

function showResetPassword(token) {
  appState.resetToken = token;
  authTabs.hidden = true;
  authForm.hidden = true;
  forgotPasswordForm.hidden = true;
  resetPasswordForm.hidden = false;
  document.querySelector("#forgot-password-link").hidden = true;
  document.querySelector("#auth-title").textContent = "Đặt mật khẩu mới.";
  document.querySelector("#auth-kicker").textContent = "KHÔI PHỤC TÀI KHOẢN";
  document.querySelector("#auth-subtitle").textContent = "Chọn mật khẩu mới có ít nhất 8 ký tự.";
  document.querySelector("#reset-password-message").hidden = true;
}

async function submitForgotPassword(event) {
  event.preventDefault();
  if (!forgotPasswordForm.reportValidity()) return;
  const button = forgotPasswordForm.querySelector("button[type=submit]");
  const message = document.querySelector("#forgot-password-message");
  button.disabled = true;
  message.hidden = true;
  try {
    const email = new FormData(forgotPasswordForm).get("email").trim();
    const result = await request("/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify({ email })
    });
    notify(message, result.message, true);
  } catch (error) {
    notify(message, error.message);
  } finally {
    button.disabled = false;
  }
}

async function submitResetPassword(event) {
  event.preventDefault();
  if (!resetPasswordForm.reportValidity()) return;
  const values = new FormData(resetPasswordForm);
  const newPassword = values.get("newPassword");
  const message = document.querySelector("#reset-password-message");
  if (newPassword !== values.get("confirmPassword")) {
    notify(message, "Hai mật khẩu chưa khớp.");
    return;
  }

  const button = resetPasswordForm.querySelector("button[type=submit]");
  button.disabled = true;
  message.hidden = true;
  try {
    await request("/auth/reset-password", {
      method: "POST",
      body: JSON.stringify({ token: appState.resetToken, newPassword })
    });
    appState.resetToken = null;
    appState.token = null;
    localStorage.removeItem("kiss.accessToken");
    localStorage.removeItem("kiss.profile");
    resetPasswordForm.reset();
    setAuthMode("login");
    notify(authMessage, "Mật khẩu đã được đổi. Hãy đăng nhập bằng mật khẩu mới.", true);
  } catch (error) {
    notify(message, error.message);
  } finally {
    button.disabled = false;
  }
}

async function submitAuth(event) {
  event.preventDefault();
  if (!authForm.reportValidity()) return;
  const values = new FormData(authForm);
  const registering = authMode === "register";
  const payload = { email: values.get("email").trim(), password: values.get("password") };
  if (registering) {
    payload.name = values.get("name").trim();
    payload.birthDate = values.get("birthDate");
    payload.gender = values.get("gender");
    payload.acceptTerms = values.get("acceptTerms") === "on";
  }

  authButton.disabled = true;
  authButton.textContent = "Đang xử lý...";
  authMessage.hidden = true;
  try {
    const session = await request(`/auth/${registering ? "register" : "login"}`, {
      method: "POST",
      body: JSON.stringify(payload)
    });
    appState.token = session.accessToken;
    localStorage.setItem("kiss.accessToken", session.accessToken);
    appState.profile = await request("/profiles/me");
    await enterWorkspace();
  } catch (error) {
    authMessage.textContent = error.message;
    authMessage.hidden = false;
  } finally {
    authButton.disabled = false;
    authButton.innerHTML = `${registering ? "Tạo tài khoản" : "Đăng nhập"} <span aria-hidden="true">↗</span>`;
  }
}

async function enterWorkspace() {
  authView.hidden = true;
  appView.hidden = false;
  document.title = "Kiss | Khám phá";
  document.querySelector("#app-user-name").textContent = appState.profile.name;
  await loadInterests();
  fillProfile();
  await Promise.all([loadDiscover(), loadMatches(), loadJourney(), loadAdminOverview()]);
  showPage("discover");
}

function showPage(page) {
  for (const id of ["discover", "journey", "profile", "matches", "chat", "admin"]) {
    document.getElementById(`${id}-page`).hidden = id !== page;
  }
  document.querySelectorAll("[data-page]").forEach((button) => {
    button.classList.toggle("is-active", button.dataset.page === page);
  });
  if (page === "discover") loadDiscover();
  if (page === "profile") loadProfile();
  if (page === "journey") loadJourney();
  if (page === "matches") loadMatches();
  if (page === "chat") loadChatContacts();
  else stopChatPolling();
  if (page === "admin") loadAdminOverview();
}

async function loadAdminOverview() {
  const nav = document.querySelector("#admin-nav");
  try {
    const overview = await request("/admin/overview");
    nav.hidden = false;
    document.querySelector("#admin-stat-grid").innerHTML = [
      ["Tài khoản", overview.userCount], ["Lượt thích/bỏ qua", overview.swipeCount],
      ["Lượt kết đôi", overview.matchCount], ["Bài viết", overview.journeyPostCount],
      ["Bình luận", overview.journeyCommentCount], ["Tin nhắn (chỉ số lượng)", overview.chatMessageCount]
    ].map(([label, value]) => `<article class="admin-stat"><span>${escapeHtml(label)}</span><strong>${value}</strong></article>`).join("");
    document.querySelector("#admin-users").innerHTML = overview.recentUsers.length
      ? `<div class="admin-user-row admin-user-heading"><span>Tên</span><span>Email</span><span>Ngày tạo</span></div>${overview.recentUsers.map((user) => `<div class="admin-user-row"><strong>${escapeHtml(user.name)}</strong><span>${escapeHtml(user.email)}</span><time>${new Date(user.createdAt).toLocaleDateString("vi-VN")}</time></div>`).join("")}`
      : '<p class="empty-message">Chưa có tài khoản.</p>';
    document.querySelector("#admin-message").hidden = true;
  } catch {
    nav.hidden = true;
  }
}

async function exportAdminData() {
  const button = document.querySelector("#admin-export");
  const message = document.querySelector("#admin-message");
  button.disabled = true;
  try {
    const data = await request("/admin/export");
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `kiss-data-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    notify(message, "Đã tải bản xuất JSON.", true);
  } catch (error) {
    notify(message, error.message);
  } finally {
    button.disabled = false;
  }
}

function notify(element, text, success = false) {
  element.textContent = text;
  element.classList.toggle("is-success", success);
  element.hidden = false;
}

async function loadInterests() {
  appState.interests = await request("/interests");
  const filter = document.querySelector("#filter-interest");
  filter.innerHTML = '<option value="">Tất cả</option>' + appState.interests.map((interest) =>
    `<option value="${interest.id}">${escapeHtml(interest.name)}</option>`).join("");
  document.querySelector("#interest-options").innerHTML = appState.interests.map((interest) => `
    <label class="interest-option"><input type="checkbox" name="interestIds" value="${interest.id}"><span>${escapeHtml(interest.name)}</span></label>`).join("");
}

async function loadDiscover() {
  const form = document.querySelector("#discover-filters");
  const values = new FormData(form);
  const params = new URLSearchParams({ limit: "30" });
  for (const key of ["search", "gender", "minAge", "maxAge", "city", "lookingFor", "interestId"]) {
    const value = values.get(key);
    if (value) params.set(key, value);
  }
  const results = document.querySelector("#discover-results");
  const message = document.querySelector("#discover-message");
  message.hidden = true;
  results.innerHTML = '<p class="empty-message">Đang tìm hồ sơ phù hợp...</p>';
  try {
    const profiles = await request(`/profiles/discover?${params.toString()}`);
    renderProfiles(results, profiles, true);
  } catch (error) {
    results.replaceChildren();
    notify(message, error.message);
  }
}

function renderProfiles(container, profiles, allowSwipe, allowChat = false) {
  if (!profiles.length) {
    container.innerHTML = '<p class="empty-message">Chưa tìm thấy hồ sơ phù hợp. Hãy thử điều chỉnh bộ lọc hoặc quay lại sau.</p>';
    return;
  }
  container.innerHTML = profiles.map((profile) => {
    const name = cleanProfileName(profile.name);
    const avatar = profile.photos?.find((photo) => photo.isAvatar) || profile.photos?.[0];
    const image = photoUrl(avatar?.url);
    return `<article class="person-card" data-profile-id="${profile.id}">
      ${image ? `<img class="person-photo" src="${escapeHtml(image)}" alt="Ảnh hồ sơ ${escapeHtml(name)}">` : `<div class="person-photo person-photo-placeholder" aria-hidden="true">${escapeHtml(name.trim().charAt(0).toUpperCase())}</div>`}
      <div class="person-card-body"><div class="person-card-heading"><h2>${escapeHtml(name)}</h2><span>${ageFrom(profile.birthDate)}</span></div>
      <div class="person-meta">${profile.city ? `<span>${escapeHtml(profile.city)}</span>` : ""}<span>${escapeHtml(profile.lookingFor || "Dating")}</span></div>
      <p class="person-bio">${escapeHtml(profile.bio || "Chưa có phần giới thiệu.")}</p>
      <div class="person-interests">${(profile.interests || []).map((interest) => `<span class="interest-chip">${escapeHtml(interest.name)}</span>`).join("")}</div>
      ${allowSwipe ? `<div class="person-card-actions"><button type="button" data-swipe="false">Bỏ qua</button><button type="button" data-swipe="true">Thích ♡</button></div>` : allowChat ? `<div class="person-card-actions person-card-chat-actions"><button class="person-card-chat-button" type="button" data-open-chat="${profile.id}" aria-label="Nhắn tin với ${escapeHtml(name)}"><svg aria-hidden="true" viewBox="0 0 20 20"><path d="M3 4.75A2.75 2.75 0 0 1 5.75 2h8.5A2.75 2.75 0 0 1 17 4.75v5.5A2.75 2.75 0 0 1 14.25 13H9l-4.5 3v-3.2A2.75 2.75 0 0 1 3 10.25z"/></svg>Nhắn tin</button></div>` : ""}</div></article>`;
  }).join("");
}

async function loadProfile() {
  try {
    appState.profile = await request("/profiles/me");
    fillProfile();
    renderPhotos();
  } catch (error) {
    notify(document.querySelector("#profile-message"), error.message);
  }
}

function fillProfile() {
  const form = document.querySelector("#profile-form");
  if (!appState.profile) return;
  for (const key of ["name", "birthDate", "gender", "city", "occupation", "education", "lookingFor", "bio"]) {
    form.elements[key].value = appState.profile[key] || "";
  }
  const interestIds = new Set((appState.profile.interests || []).map((interest) => interest.id));
  document.querySelectorAll("input[name=interestIds]").forEach((input) => {
    input.checked = interestIds.has(input.value);
  });
}

function renderPhotos() {
  const photos = appState.profile?.photos || [];
  document.querySelector("#photo-list").innerHTML = photos.length
    ? photos.map((photo) => `<div class="photo-row" data-photo-id="${photo.id}"><img src="${escapeHtml(photoUrl(photo.url))}" alt="Ảnh hồ sơ"><span>${photo.isAvatar ? "Ảnh đại diện" : "Ảnh hồ sơ"}</span>${photo.isAvatar ? "" : `<button type="button" data-avatar>Đặt đại diện</button>`}<button type="button" data-delete-photo>Xóa</button></div>`).join("")
    : '<p class="empty-message">Bạn chưa thêm ảnh hồ sơ.</p>';
}

async function saveProfile(event) {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  const message = document.querySelector("#profile-message");
  message.hidden = true;
  try {
    appState.profile = await request("/profiles/me", {
      method: "PUT",
      body: JSON.stringify(Object.fromEntries(["name", "birthDate", "gender", "city", "occupation", "education", "lookingFor", "bio"].map((key) => [key, form.get(key)])))
    });
    const interestIds = [...document.querySelectorAll("input[name=interestIds]:checked")].map((input) => input.value);
    await request("/profiles/me/interests", { method: "PUT", body: JSON.stringify({ interestIds }) });
    appState.profile = await request("/profiles/me");
    fillProfile();
    renderPhotos();
    notify(message, "Đã lưu hồ sơ và sở thích.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function uploadPhoto(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const message = document.querySelector("#profile-message");
  try {
    const photo = await request("/profiles/me/photos", { method: "POST", body: new FormData(form) });
    form.reset();
    appState.profile.photos = [...(appState.profile.photos || []), photo];
    renderPhotos();
    notify(message, "Đã tải ảnh lên.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function managePhoto(event) {
  const button = event.target.closest("[data-avatar], [data-delete-photo]");
  if (!button) return;
  const row = button.closest("[data-photo-id]");
  const photoId = row.dataset.photoId;
  const message = document.querySelector("#profile-message");
  try {
    if (button.hasAttribute("data-avatar")) {
      await request(`/profiles/me/photos/${photoId}/avatar`, { method: "PUT" });
    } else {
      await request(`/profiles/me/photos/${photoId}`, { method: "DELETE" });
    }
    await loadProfile();
    notify(message, button.hasAttribute("data-avatar") ? "Đã đổi ảnh đại diện." : "Đã xóa ảnh.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function swipe(event) {
  const button = event.target.closest("[data-swipe]");
  if (!button) return;
  const card = button.closest("[data-profile-id]");
  const buttons = card.querySelectorAll("button");
  buttons.forEach((item) => { item.disabled = true; });
  try {
    const result = await request("/swipes", {
      method: "POST",
      body: JSON.stringify({ targetUserId: card.dataset.profileId, isLike: button.dataset.swipe === "true" })
    });
    card.remove();
    if (result.isMatch) {
      await loadMatches();
      notify(document.querySelector("#discover-message"), "Hai bạn đã thích nhau! Đã có một kết đôi mới.", true);
    }
    if (!document.querySelector("#discover-results .person-card")) {
      document.querySelector("#discover-results").innerHTML = '<p class="empty-message">Đã xem hết các hồ sơ trong kết quả lọc.</p>';
    }
  } catch (error) {
    buttons.forEach((item) => { item.disabled = false; });
    notify(document.querySelector("#discover-message"), error.message);
  }
}

async function loadMatches() {
  try {
    const matches = await request("/matches");
    document.querySelector("#match-count").textContent = matches.length;
    renderProfiles(document.querySelector("#matches-results"), matches.map((match) => match.profile), false, true);
  } catch (error) {
    notify(document.querySelector("#discover-message"), error.message);
  }
}

async function openChatFromMatch(event) {
  const button = event.target.closest("[data-open-chat]");
  if (!button || !document.querySelector("#matches-page").contains(button)) return;
  chatState.pendingUserId = button.dataset.openChat;
  showPage("chat");
}

async function loadChatContacts() {
  const list = document.querySelector("#chat-contact-list");
  const status = document.querySelector("#chat-status");
  try {
    const matches = await request("/matches");
    chatState.contacts = matches.map((match) => match.profile);
    document.querySelector("#match-count").textContent = matches.length;
    document.querySelector("#chat-contact-count").textContent = matches.length;

    if (!chatState.contacts.length) {
      list.innerHTML = '<p class="chat-contact-empty">Khi có kết đôi, bạn có thể bắt đầu nhắn tin tại đây.</p>';
      chatState.pendingUserId = null;
      clearActiveChat();
      return;
    }

    renderChatContacts();
    const preferredId = chatState.pendingUserId || chatState.activeUserId || chatState.contacts[0].id;
    chatState.pendingUserId = null;
    const preferredContact = chatState.contacts.find((profile) => profile.id === preferredId);
    if (preferredContact) {
      await selectChatContact(preferredContact.id);
    } else {
      clearActiveChat();
    }
  } catch (error) {
    list.innerHTML = `<p class="chat-contact-empty">${escapeHtml(error.message)}</p>`;
    notify(status, error.message);
  }
}

function renderChatContacts() {
  const list = document.querySelector("#chat-contact-list");
  list.innerHTML = chatState.contacts.map((profile) => {
    const name = cleanProfileName(profile.name);
    const avatar = profile.photos?.find((photo) => photo.isAvatar) || profile.photos?.[0];
    const image = photoUrl(avatar?.url);
    const initials = (name || "K").trim().charAt(0).toUpperCase();
    return `<button class="chat-contact ${chatState.activeUserId === profile.id ? "is-active" : ""}" data-chat-user="${profile.id}" type="button">
      ${image ? `<img class="chat-contact-avatar" src="${escapeHtml(image)}" alt="">` : `<span class="chat-contact-avatar chat-contact-initial">${escapeHtml(initials)}</span>`}
      <span class="chat-contact-copy"><strong>${escapeHtml(name)}</strong><small>Nhấn để trò chuyện</small></span><span class="chat-contact-arrow" aria-hidden="true">›</span>
    </button>`;
  }).join("");
}

async function selectChatContact(userId) {
  const profile = chatState.contacts.find((item) => item.id === userId);
  if (!profile) return;
  const changed = chatState.activeUserId !== userId;
  if (changed) {
    clearChatMediaUrls();
    chatState.messages = [];
    chatState.lastMessageKey = null;
    document.querySelector("#chat-form").reset();
    updateChatFileName();
  }
  chatState.activeUserId = userId;
  document.querySelector("#chat-empty").hidden = true;
  document.querySelector("#chat-thread-header").hidden = false;
  document.querySelector("#chat-form").hidden = false;
  const name = cleanProfileName(profile.name);
  document.querySelector("#chat-peer-name").textContent = name;
  const avatar = profile.photos?.find((photo) => photo.isAvatar) || profile.photos?.[0];
  const image = photoUrl(avatar?.url);
  const avatarElement = document.querySelector("#chat-peer-avatar");
  avatarElement.innerHTML = image
    ? `<img src="${escapeHtml(image)}" alt="">`
    : escapeHtml((name || "K").trim().charAt(0).toUpperCase());
  document.querySelector("#chat-status").hidden = true;
  renderChatContacts();
  await loadChatMessages(changed);
  if (!document.querySelector("#chat-page").hidden) startChatPolling();
}

function clearActiveChat() {
  stopChatPolling();
  chatState.activeUserId = null;
  chatState.messages = [];
  chatState.lastMessageKey = null;
  clearChatMediaUrls();
  document.querySelector("#chat-empty").hidden = false;
  document.querySelector("#chat-thread-header").hidden = true;
  document.querySelector("#chat-form").hidden = true;
  document.querySelector("#chat-form").reset();
  updateChatFileName();
  document.querySelector("#chat-status").hidden = true;
  document.querySelector("#chat-messages").replaceChildren();
}

function clearChatMediaUrls() {
  for (const url of chatState.mediaUrls.values()) {
    if (typeof url === "string" && url.startsWith("blob:")) URL.revokeObjectURL(url);
  }
  chatState.mediaUrls.clear();
}

function stopChatPolling() {
  if (chatState.pollTimer) clearInterval(chatState.pollTimer);
  chatState.pollTimer = null;
}

function startChatPolling() {
  stopChatPolling();
  if (!chatState.activeUserId) return;
  chatState.pollTimer = setInterval(() => loadChatMessages(false), 3000);
}

async function loadChatAttachment(message) {
  if (!message.mediaUrl || chatState.mediaUrls.has(message.id)) return;
  const conversationId = chatState.activeUserId;
  chatState.mediaUrls.set(message.id, null);
  try {
    const headers = new Headers();
    if (appState.token) headers.set("Authorization", `Bearer ${appState.token}`);
    const response = await fetch(message.mediaUrl, { headers });
    if (!response.ok) throw new Error("Không tải được tệp đính kèm.");
    const objectUrl = URL.createObjectURL(await response.blob());
    if (chatState.activeUserId !== conversationId) {
      URL.revokeObjectURL(objectUrl);
      return;
    }
    chatState.mediaUrls.set(message.id, objectUrl);
  } catch {
    if (chatState.activeUserId === conversationId) chatState.mediaUrls.set(message.id, "");
  }
}

async function loadChatMessages(scrollToLatest = false) {
  if (!chatState.activeUserId || chatState.isLoading) return;
  chatState.isLoading = true;
  const container = document.querySelector("#chat-messages");
  const conversationId = chatState.activeUserId;
  const wasNearBottom = container.scrollHeight - container.scrollTop - container.clientHeight < 80;
  try {
    const messages = await request(`/chat/${conversationId}/messages`);
    if (chatState.activeUserId !== conversationId) return;
    const key = messages.map((message) => message.id).join("|");
    if (key === chatState.lastMessageKey) return;
    chatState.messages = messages;
    await Promise.all(messages.map(loadChatAttachment));
    chatState.lastMessageKey = key;
    renderChatMessages(scrollToLatest || wasNearBottom);
  } catch (error) {
    notify(document.querySelector("#chat-status"), error.message);
  } finally {
    chatState.isLoading = false;
  }
}

function renderChatMessages(scrollToLatest) {
  const container = document.querySelector("#chat-messages");
  if (!chatState.messages.length) {
    container.innerHTML = '<p class="chat-no-messages">Chưa có tin nhắn. Hãy gửi lời chào đầu tiên nhé.</p>';
    return;
  }

  container.innerHTML = chatState.messages.map((message) => {
    const mediaUrl = chatState.mediaUrls.get(message.id);
    const mediaMarkup = message.mediaUrl
      ? mediaUrl
        ? message.mediaType?.startsWith("video/")
          ? `<video class="chat-message-media" src="${escapeHtml(mediaUrl)}" controls playsinline preload="metadata"></video>`
          : `<img class="chat-message-media" src="${escapeHtml(mediaUrl)}" alt="Ảnh được gửi trong cuộc trò chuyện">`
        : `<span class="chat-attachment-status">${mediaUrl === "" ? "Không tải được tệp đính kèm." : "Đang tải tệp..."}</span>`
      : "";
    const time = new Date(message.sentAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
    return `<article class="chat-message-row ${message.isMine ? "is-mine" : ""}" data-chat-message="${message.id}">
      <div class="chat-message-bubble">${message.content ? `<p>${escapeHtml(message.content)}</p>` : ""}${mediaMarkup}<time>${time}</time></div>
    </article>`;
  }).join("");
  if (scrollToLatest) container.scrollTop = container.scrollHeight;
}

function updateChatFileName() {
  const input = document.querySelector("#chat-file");
  const label = document.querySelector("#chat-file-name");
  const clearButton = document.querySelector("#chat-file-clear");
  const file = input.files?.[0];
  label.textContent = file ? file.name : "Tối đa 8 MB cho ảnh, 20 MB cho video";
  clearButton.hidden = !file;
}

function insertChatEmoji(event) {
  const button = event.target.closest("[data-chat-emoji]");
  if (!button) return;
  const input = document.querySelector("#chat-content");
  const emoji = button.dataset.chatEmoji;
  input.setRangeText(emoji, input.selectionStart, input.selectionEnd, "end");
  input.focus();
}

async function submitChatMessage(event) {
  event.preventDefault();
  if (!chatState.activeUserId) return;
  const input = document.querySelector("#chat-content");
  const fileInput = document.querySelector("#chat-file");
  const file = fileInput.files?.[0];
  const content = input.value.trim();
  if (!content && !file) return;

  const form = event.currentTarget;
  const button = form.querySelector("[type=submit]");
  const formData = new FormData();
  formData.set("content", content);
  if (file) formData.set("media", file);
  button.disabled = true;
  document.querySelector("#chat-status").hidden = true;
  try {
    await request(`/chat/${chatState.activeUserId}/messages`, { method: "POST", body: formData });
    input.value = "";
    fileInput.value = "";
    updateChatFileName();
    document.querySelector("#chat-emoji-picker").hidden = true;
    document.querySelector("#chat-emoji-toggle").setAttribute("aria-expanded", "false");
    await loadChatMessages(true);
  } catch (error) {
    notify(document.querySelector("#chat-status"), error.message);
  } finally {
    button.disabled = false;
    input.focus();
  }
}

function updateJourneyPreview() {
  const input = document.querySelector("#journey-media");
  const preview = document.querySelector("#journey-media-preview");
  const fileName = document.querySelector("#journey-file-name");
  const file = input.files?.[0];
  if (!file) {
    preview.hidden = true;
    preview.innerHTML = "";
    fileName.textContent = "PNG, JPEG, WebP hoặc MP4, WebM";
    return;
  }

  fileName.textContent = file.name;
  const url = URL.createObjectURL(file);
  const mediaMarkup = file.type.startsWith("video/")
    ? `<video src="${url}" controls playsinline></video>`
    : `<img src="${url}" alt="Xem trước media hành trình" />`;

  preview.hidden = false;
  preview.innerHTML = mediaMarkup;
}

function clearJourneyPreview() {
  const input = document.querySelector("#journey-media");
  const preview = document.querySelector("#journey-media-preview");
  const fileName = document.querySelector("#journey-file-name");
  if (input) input.value = "";
  preview.hidden = true;
  preview.innerHTML = "";
  fileName.textContent = "PNG, JPEG, WebP hoặc MP4, WebM";
}

async function submitJourneyPost(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const message = document.querySelector("#journey-message");
  const fileInput = document.querySelector("#journey-media");
  const formData = new FormData(form);
  if (fileInput.files?.[0]) {
    formData.set("media", fileInput.files[0]);
  }
  message.hidden = true;

  try {
    const result = await request("/journey", {
      method: "POST",
      body: formData
    });
    form.reset();
    clearJourneyPreview();
    await loadJourney();
    notify(message, result ? "Bài viết của bạn đã xuất hiện trên hành trình." : "Đã đăng lên hành trình.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

async function loadJourney() {
  const feed = document.querySelector("#journey-feed");
  try {
    const posts = await request("/journey?limit=20");
    if (!posts.length) {
      feed.innerHTML = '<p class="empty-message">Chưa có bài viết nào trong hành trình. Hãy chia sẻ điều đầu tiên của bạn.</p>';
      return;
    }

    feed.innerHTML = posts.map((post) => {
      const authorName = cleanProfileName(post.authorName);
      const authorImage = photoUrl(post.authorPhotoUrl);
      const media = post.mediaUrl
        ? post.mediaType?.startsWith("video/")
          ? `<video class="journey-media" src="${escapeHtml(post.mediaUrl)}" controls playsinline></video>`
          : `<img class="journey-media" src="${escapeHtml(post.mediaUrl)}" alt="Ảnh hành trình của ${escapeHtml(authorName)}">`
        : "";
      const deleteButton = post.isMine
        ? `<button class="journey-delete" data-journey-delete="${post.id}" type="button">Xóa</button>`
        : "";
      const likeLabel = post.isLikedByMe ? "Đã thích" : "Thích";
      const initials = (authorName || "K").trim().charAt(0).toUpperCase();
      return `
        <article class="journey-card" data-journey-post-id="${post.id}">
          <header class="journey-header">
            <div class="journey-author-avatar">${authorImage ? `<img src="${escapeHtml(authorImage)}" alt="">` : escapeHtml(initials)}</div>
            <div class="journey-author-meta">
              <strong>${escapeHtml(authorName)}</strong>
              <time>${new Date(post.createdAt).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" })}</time>
            </div>
            ${deleteButton}
          </header>
          ${post.content ? `<p class="journey-content">${escapeHtml(post.content)}</p>` : ""}
          ${media}
          <footer class="journey-actions">
            <button class="journey-action-button ${post.isLikedByMe ? "is-liked" : ""}" data-journey-like="${post.id}" type="button">${likeLabel} (${post.likesCount ?? 0})</button>
            <button class="journey-action-button" data-journey-comment-toggle="${post.id}" type="button">Bình luận (${post.commentsCount ?? 0})</button>
          </footer>
          <div class="journey-comment-box" data-journey-comments="${post.id}" hidden>
            <div class="journey-comments" data-journey-comment-list="${post.id}"></div>
            <form class="journey-comment-form" data-journey-comment-form="${post.id}">
              <input name="content" maxlength="500" placeholder="Viết bình luận..." required>
              <button type="submit">Gửi</button>
            </form>
          </div>
        </article>`;
    }).join("");
    document.querySelectorAll("[data-journey-comment-toggle]").forEach((button) => button.addEventListener("click", async () => {
      const postId = button.dataset.journeyCommentToggle;
      const box = document.querySelector(`[data-journey-comments="${postId}"]`);
      if (!box) return;
      const nextState = box.hidden;
      box.hidden = !nextState;
      if (nextState) await loadJourneyComments(postId);
    }));
    document.querySelectorAll("[data-journey-like]").forEach((button) => button.addEventListener("click", () => toggleJourneyLike(button.dataset.journeyLike)));
    document.querySelectorAll("[data-journey-comment-form]").forEach((form) => form.addEventListener("submit", submitJourneyComment));
  } catch (error) {
    feed.innerHTML = `<p class="empty-message">${escapeHtml(error.message)}</p>`;
  }
}

async function loadJourneyComments(postId) {
  const list = document.querySelector(`[data-journey-comment-list="${postId}"]`);
  if (!list) return;
  try {
    const comments = await request(`/journey/${postId}/comments`);
    list.innerHTML = comments.length
      ? comments.map((comment) => `
          <div class="journey-comment-item">
            <strong>${escapeHtml(cleanProfileName(comment.authorName))}</strong>
            <span>${escapeHtml(comment.content)}</span>
          </div>`).join("")
      : '<p class="journey-empty-comment">Chưa có bình luận nào.</p>';
  } catch (error) {
    list.innerHTML = `<p class="journey-empty-comment">${escapeHtml(error.message)}</p>`;
  }
}

async function toggleJourneyLike(postId) {
  try {
    const result = await request(`/journey/${postId}/like`, { method: "POST" });
    await loadJourney();
    notify(document.querySelector("#journey-message"), result.liked ? "Bạn đã thích bài này." : "Bạn đã bỏ thích bài này.", true);
  } catch (error) {
    notify(document.querySelector("#journey-message"), error.message);
  }
}

async function submitJourneyComment(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const postId = form.dataset.journeyCommentForm;
  const content = new FormData(form).get("content");
  if (!content || !String(content).trim()) return;
  try {
    await request(`/journey/${postId}/comments`, {
      method: "POST",
      body: JSON.stringify({ content: String(content).trim() })
    });
    form.reset();
    await loadJourney();
    const box = document.querySelector(`[data-journey-comments="${postId}"]`);
    if (box) box.hidden = false;
    notify(document.querySelector("#journey-message"), "Bình luận đã được gửi.", true);
  } catch (error) {
    notify(document.querySelector("#journey-message"), error.message);
  }
}

async function deleteJourneyPost(event) {
  const button = event.target.closest("[data-journey-delete]");
  if (!button) return;
  const message = document.querySelector("#journey-message");
  try {
    await request(`/journey/${button.dataset.journeyDelete}`, { method: "DELETE" });
    await loadJourney();
    notify(message, "Đã xóa bài viết của bạn.", true);
  } catch (error) {
    notify(message, error.message);
  }
}

function logout() {
  stopChatPolling();
  clearChatMediaUrls();
  chatState.contacts = [];
  chatState.activeUserId = null;
  chatState.pendingUserId = null;
  localStorage.removeItem("kiss.accessToken");
  localStorage.removeItem("kiss.profile");
  appState.token = null;
  appState.profile = null;
  appView.hidden = true;
  authView.hidden = false;
  document.title = "Kiss | Đăng nhập";
  authForm.reset();
  setAuthMode("login");
}

async function startApp() {
  document.querySelector("#login-tab").addEventListener("click", () => setAuthMode("login"));
  document.querySelector("#register-tab").addEventListener("click", () => setAuthMode("register"));
  authForm.addEventListener("submit", submitAuth);
  document.querySelector("#discover-filters").addEventListener("submit", (event) => { event.preventDefault(); loadDiscover(); });
  document.querySelector("#profile-form").addEventListener("submit", saveProfile);
  document.querySelector("#photo-form").addEventListener("submit", uploadPhoto);
  document.querySelector("#photo-list").addEventListener("click", managePhoto);
  document.querySelector("#discover-results").addEventListener("click", swipe);
  document.querySelector("#matches-results").addEventListener("click", openChatFromMatch);
  document.querySelector("#chat-contact-list").addEventListener("click", (event) => {
    const contact = event.target.closest("[data-chat-user]");
    if (contact) selectChatContact(contact.dataset.chatUser);
  });
  document.querySelector("#chat-form").addEventListener("submit", submitChatMessage);
  document.querySelector("#chat-file").addEventListener("change", updateChatFileName);
  document.querySelector("#chat-file-clear").addEventListener("click", () => {
    document.querySelector("#chat-file").value = "";
    updateChatFileName();
  });
  document.querySelector("#chat-emoji-toggle").addEventListener("click", (event) => {
    const picker = document.querySelector("#chat-emoji-picker");
    picker.hidden = !picker.hidden;
    event.currentTarget.setAttribute("aria-expanded", String(!picker.hidden));
  });
  document.querySelector("#chat-emoji-picker").addEventListener("click", insertChatEmoji);
  document.querySelector("#chat-content").addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      event.currentTarget.form.requestSubmit();
    }
  });
  document.querySelector("#journey-form").addEventListener("submit", submitJourneyPost);
  document.querySelector("#journey-media").addEventListener("change", updateJourneyPreview);
  document.querySelector("#journey-refresh").addEventListener("click", loadJourney);
  document.querySelector("#journey-feed").addEventListener("click", deleteJourneyPost);
  document.querySelectorAll("[data-page]").forEach((button) => button.addEventListener("click", () => showPage(button.dataset.page)));
  document.querySelector("#logout-button").addEventListener("click", logout);
  document.querySelector("#forgot-password-link").addEventListener("click", showForgotPassword);
  document.querySelector("#forgot-password-back").addEventListener("click", () => setAuthMode("login"));
  document.querySelector("#reset-password-back").addEventListener("click", () => setAuthMode("login"));
  forgotPasswordForm.addEventListener("submit", submitForgotPassword);
  resetPasswordForm.addEventListener("submit", submitResetPassword);
  document.querySelector("#admin-refresh").addEventListener("click", loadAdminOverview);
  document.querySelector("#admin-export").addEventListener("click", exportAdminData);

  const resetUrl = new URL(window.location.href);
  const resetToken = resetUrl.searchParams.get("resetToken");
  if (resetToken) {
    resetUrl.searchParams.delete("resetToken");
    window.history.replaceState({}, "", `${resetUrl.pathname}${resetUrl.search}${resetUrl.hash}`);
    showResetPassword(resetToken);
    return;
  }

  if (!appState.token) return;
  try {
    appState.profile = await request("/profiles/me");
    await enterWorkspace();
  } catch {
    logout();
  }
}

startApp();
