const authView = document.querySelector(".auth-layout");
const authForm = document.querySelector("#auth-form");
const authTabs = document.querySelector(".auth-tabs");
const forgotPasswordForm = document.querySelector("#forgot-password-form");
const resetPasswordForm = document.querySelector("#reset-password-form");
const appView = document.querySelector("#dating-app");
const authMessage = document.querySelector("#auth-message");
const authButton = document.querySelector("#auth-submit");
const appState = { token: localStorage.getItem("kiss.accessToken"), profile: null, interests: [], resetToken: null, connections: [], conversations: [], selectedConversationId: null, chatPoll: null, viewedProfile: null, rooms: [] };
const roomState = { connection: null, activeRoomId: null };
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

const escapeHtml = (value = "") => String(value).normalize("NFC").replace(/[&<>"']/g, (character) => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
}[character]));

function photoUrl(value) {
  return typeof value === "string" && (value.startsWith("/uploads/") || value.startsWith("/demo-profiles/")) ? value : "";
}

function emptyState(title, description, actionLabel = "Khám phá hồ sơ ngay", actionPage = "discover") {
  return `<div class="empty-state"><div class="empty-state-art" aria-hidden="true"><svg viewBox="0 0 48 48" fill="none"><path d="M24 39S7 29.5 7 17.8C7 12.4 11 9 15.7 9c3.4 0 6.4 2 8.3 5 1.9-3 4.9-5 8.3-5C37 9 41 12.4 41 17.8 41 29.5 24 39 24 39Z" fill="currentColor" opacity=".15"/><path d="M24 36S9 27.8 9 17.9C9 13.2 12.2 10 16.3 10c3.4 0 6.1 2.1 7.7 5.2C25.6 12.1 28.3 10 31.7 10 35.8 10 39 13.2 39 17.9 39 27.8 24 36 24 36Z" stroke="currentColor" stroke-width="2"/></svg></div><h3>${escapeHtml(title)}</h3><p>${escapeHtml(description)}</p><button class="empty-state-action" type="button" data-empty-page="${escapeHtml(actionPage)}">${escapeHtml(actionLabel)}</button></div>`;
}

function cleanProfileName(value = "") {
  return String(value).normalize("NFC").replace(/^\[Mẫu\]\s*/u, "");
}

function lookingForLabel(value) {
  switch (value) {
    case "Friendship": return "Kết bạn";
    case "Dating": return "Hẹn hò";
    case "Relationship": return "Mối quan hệ nghiêm túc";
    default: return "Chưa cập nhật";
  }
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
  if (!response.ok) {
    const error = new Error(result?.detail || result?.title || "Không thể hoàn tất yêu cầu.");
    error.status = response.status;
    throw error;
  }
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
  document.querySelector("#app-user-avatar").textContent = cleanProfileName(appState.profile.name).trim().charAt(0).toUpperCase() || "K";
  await loadInterests();
  fillProfile();
  await Promise.all([loadDiscover(), loadMatches(), loadJourney(), loadAdminOverview(), loadRooms()]);
  showPage("discover");
}

function showPage(page) {
  page = page === "messages" ? "chat" : page;
  for (const id of ["discover", "view-profile", "journey", "profile", "matches", "chat", "rooms", "connections", "messages", "admin"]) {
    document.getElementById(`${id}-page`).hidden = id !== page;
  }
  document.querySelectorAll("[data-page]").forEach((button) => {
    button.classList.toggle("is-active", button.dataset.page === page);
  });
  if (page === "discover") loadDiscover();
  if (page === "profile") loadProfile();
  if (page === "journey") loadJourney();
  if (page === "matches") loadMatches();
  if (page === "connections") loadConnections();
  if (page === "chat") loadChatContacts();
  else stopChatPolling();
  if (page === "rooms") loadRooms();
  if (page === "messages") startSocialPolling();
  else stopSocialPolling();
  if (page === "admin") loadAdminOverview();
}

async function loadRooms() {
  const form = document.querySelector("#rooms-filter-form");
  const results = document.querySelector("#rooms-results");
  const loading = document.querySelector("#rooms-loading");
  const message = document.querySelector("#rooms-message");
  if (!results) return;
  loading.hidden = false;
  message.hidden = true;
  try {
    const params = new URLSearchParams();
    if (form.elements.search.value.trim()) params.set("search", form.elements.search.value.trim());
    if (form.elements.topic.value.trim()) params.set("topic", form.elements.topic.value.trim());
    appState.rooms = await request(`/rooms?${params}`);
    results.innerHTML = appState.rooms.length ? appState.rooms.map(renderRoomCard).join("") : emptyState("Chưa có phòng nào", "Hãy tạo một không gian thật chill cho mọi người.", "Tạo phòng đầu tiên", "rooms");
  } catch (error) {
    message.textContent = error.message;
    message.hidden = false;
  } finally { loading.hidden = true; }
}

function renderRoomCard(room) {
  const image = photoUrl(room.avatarUrl);
  return `<article class="room-card"><div class="room-card-avatar">${image ? `<img src="${escapeHtml(image)}" alt="">` : "💬"}</div><div class="room-card-content"><div class="room-card-title"><h2>${escapeHtml(room.name)}</h2>${room.isPrivate ? "<span class=room-private>Riêng tư</span>" : ""}</div><p class="room-topic">${escapeHtml(room.topic || "Cùng trò chuyện")}</p><p>${escapeHtml(room.description || "Một góc nhỏ để gặp gỡ những người cùng vibe.")}</p><div class="room-card-footer"><span>👥 ${room.memberCount}/${room.maxMembers}</span><button class="submit-button room-join-button" type="button" data-room-join="${room.id}">Vào phòng</button></div></div></article>`;
}

async function joinRoom(roomId) {
  try { await request(`/rooms/${roomId}/join`, { method: "POST", body: JSON.stringify({}) }); showPage("rooms"); }
  catch (error) { const message = document.querySelector("#rooms-message"); message.textContent = error.message; message.hidden = false; }
}

function toggleRoomInvite() {
  const checkbox = document.querySelector("#create-room-form [name=isPrivate]");
  document.querySelector("#room-invite-field").hidden = !checkbox.checked;
  document.querySelector("#create-room-form [name=inviteCode]").required = checkbox.checked;
}

async function submitCreateRoom(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const message = document.querySelector("#create-room-message");
  try {
    const data = Object.fromEntries(new FormData(form).entries());
    data.isPrivate = form.elements.isPrivate.checked;
    data.maxMembers = Number(data.maxMembers || 50);
    await request("/rooms", { method: "POST", body: JSON.stringify(data) });
    document.querySelector("#create-room-dialog").close();
    form.reset(); toggleRoomInvite(); loadRooms();
  } catch (error) { message.textContent = error.message; message.hidden = false; }
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

function socialAvatar(user, className = "social-avatar") {
  const initial = escapeHtml((user.name || "?").trim().charAt(0).toUpperCase());
  return `<div class="${className}">${user.avatarUrl ? `<img src="${escapeHtml(photoUrl(user.avatarUrl))}" alt="">` : initial}</div>`;
}

function socialCard(user, action = "") {
  return `<article class="social-user-card" data-social-user="${user.id}">${socialAvatar(user)}<div class="social-user-copy"><strong>${escapeHtml(user.name)}</strong><p>${escapeHtml([user.city, user.bio].filter(Boolean).join(" · ") || "Chưa có giới thiệu")}</p></div>${action}</article>`;
}

async function loadConnections(searchTerm) {
  const message = document.querySelector("#social-message");
  try {
    const [connections, users] = await Promise.all([
      request("/connections"),
      searchTerm === undefined ? Promise.resolve(null) : request(`/users/search?q=${encodeURIComponent(searchTerm)}`)
    ]);
    appState.connections = connections;
    const incoming = connections.filter((item) => item.status === "Pending" && !item.requestedByMe);
    const outgoing = connections.filter((item) => item.status === "Pending" && item.requestedByMe);
    const friends = connections.filter((item) => item.status === "Accepted");
    document.querySelector("#connection-count").textContent = friends.length;
    document.querySelector("#incoming-requests").innerHTML = incoming.length ? incoming.map((item) => socialCard(item.user, `<div><button class="social-action primary" data-connection-accept="${item.id}" type="button">Chấp nhận</button> <button class="social-action danger" data-connection-decline="${item.id}" type="button">Từ chối</button></div>`)).join("") : emptyState("Chưa có lời mời", "Khi có người muốn kết nối, lời mời sẽ xuất hiện ở đây.");
    document.querySelector("#outgoing-requests").innerHTML = outgoing.length ? outgoing.map((item) => socialCard(item.user, `<button class="social-action" data-connection-cancel="${item.id}" type="button">Đã gửi · Hủy</button>`)).join("") : emptyState("Chưa gửi lời mời", "Khám phá thêm hồ sơ để tìm người có cùng sở thích.");
    document.querySelector("#friends-list").innerHTML = friends.length ? friends.map((item) => socialCard(item.user, `<button class="social-action danger" data-connection-disconnect="${item.id}" type="button">Hủy kết nối</button>`)).join("") : emptyState("Chưa có kết nối nào", "Khám phá những người cùng nhịp và bắt đầu một lời chào.");
    if (users) {
      const statusByUser = new Map(connections.map((item) => [item.user.id, item]));
      const results = users.filter((user) => !connections.some((item) => item.user.id === user.id && ["Accepted", "Pending"].includes(item.status)));
      document.querySelector("#people-results").innerHTML = results.length ? results.map((user) => {
        const previous = statusByUser.get(user.id);
        const action = previous?.status === "Pending" ? '<button class="social-action" disabled>Đang chờ</button>' : `<button class="social-action primary" data-connection-request="${user.id}" type="button">Kết nối</button>`;
        return socialCard(user, action);
      }).join("") : '<p class="empty-message">Không tìm thấy người dùng phù hợp.</p>';
    }
    message.hidden = true;
  } catch (error) { notify(message, error.message); }
}

async function manageConnection(event) {
  const button = event.target.closest("[data-connection-request], [data-connection-accept], [data-connection-decline], [data-connection-cancel], [data-connection-disconnect]");
  if (!button) return;
  if (button.hasAttribute("data-connection-disconnect") && !window.confirm("Bạn có chắc muốn hủy kết nối này? Lịch sử tin nhắn sẽ được giữ lại nhưng không thể tiếp tục nhắn tin.")) return;
  button.disabled = true;
  try {
    if (button.dataset.connectionRequest) await request(`/connections/${button.dataset.connectionRequest}`, { method: "POST" });
    if (button.dataset.connectionAccept) await request(`/connections/${button.dataset.connectionAccept}/accept`, { method: "POST" });
    if (button.dataset.connectionDecline) await request(`/connections/${button.dataset.connectionDecline}/decline`, { method: "POST" });
    if (button.dataset.connectionCancel) await request(`/connections/${button.dataset.connectionCancel}`, { method: "DELETE" });
    if (button.dataset.connectionDisconnect) await request(`/connections/${button.dataset.connectionDisconnect}/disconnect`, { method: "POST" });
    await loadConnections(document.querySelector("#people-search-form").elements.q.value.trim());
    if (button.hasAttribute("data-connection-accept") || button.hasAttribute("data-connection-disconnect")) await loadConversations();
  } catch (error) { notify(document.querySelector("#social-message"), error.message); button.disabled = false; }
}

function renderConversations() {
  const list = document.querySelector("#conversation-list");
  const unreadTotal = appState.conversations.reduce((sum, item) => sum + item.unreadCount, 0);
  document.querySelector("#message-count").textContent = unreadTotal;
  list.innerHTML = appState.conversations.length ? appState.conversations.map((item) => `<button class="conversation-item ${item.id === appState.selectedConversationId ? "is-active" : ""}" data-open-conversation="${item.id}" type="button">${socialAvatar(item.user)}<span class="conversation-copy"><strong>${escapeHtml(item.user.name)}</strong><span>${escapeHtml(item.lastMessage || "Bắt đầu cuộc trò chuyện")}</span></span>${item.unreadCount ? `<span class="unread-count">${item.unreadCount}</span>` : ""}</button>`).join("") : '<p class="chat-empty">Bạn chưa có cuộc trò chuyện. Hãy kết nối với bạn bè trước.</p>';
}

async function loadConversations() {
  try {
    appState.conversations = await request("/conversations");
    if (!appState.selectedConversationId || !appState.conversations.some((item) => item.id === appState.selectedConversationId)) {
      appState.selectedConversationId = appState.conversations[0]?.id || null;
    }
    renderConversations();
    if (appState.selectedConversationId) await loadSocialMessages();
    else {
      document.querySelector("#social-chat-heading").innerHTML = '<div class="chat-empty">Chọn một người bạn để bắt đầu trò chuyện.</div>';
      document.querySelector("#social-chat-messages").innerHTML = emptyState("Chưa có cuộc trò chuyện", "Chọn một người bạn để bắt đầu nhắn tin.");
      document.querySelector("#social-chat-form").hidden = true;
    }
  } catch (error) { notify(document.querySelector("#social-chat-message"), error.message); }
}

async function loadSocialMessages() {
  const selected = appState.conversations.find((item) => item.id === appState.selectedConversationId);
  if (!selected) return;
  document.querySelector("#social-chat-heading").innerHTML = socialCard(selected.user);
  document.querySelector("#social-chat-form").hidden = false;
  try {
    const messages = await request(`/conversations/${selected.id}/messages?limit=50`);
    const container = document.querySelector("#social-chat-messages");
    container.innerHTML = messages.length ? messages.map((item) => `<article class="message-bubble ${item.senderId === appState.profile.id ? "is-mine" : ""}">${escapeHtml(item.content)}<time>${new Date(item.sentAt).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" })}${item.senderId === appState.profile.id ? (item.isRead ? " · Đã đọc" : " · Đã gửi") : ""}</time></article>`).join("") : emptyState("Bắt đầu bằng một lời chào", "Một tin nhắn ngắn có thể mở đầu cho câu chuyện thú vị.", "Viết lời chào");
    container.scrollTop = container.scrollHeight;
    const current = appState.conversations.find((item) => item.id === selected.id);
    if (current) current.unreadCount = 0;
    renderConversations();
  } catch (error) { notify(document.querySelector("#social-chat-message"), error.message); }
}

function startSocialPolling() {
  stopSocialPolling();
  loadConversations();
  appState.chatPoll = window.setInterval(loadConversations, 4000);
}

function stopSocialPolling() {
  if (appState.chatPoll) window.clearInterval(appState.chatPoll);
  appState.chatPoll = null;
}

async function submitSocialMessage(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const input = form.elements.content;
  const content = input.value.trim();
  if (!content || !appState.selectedConversationId) return;
  const button = form.querySelector("button");
  button.disabled = true;
  try {
    await request(`/conversations/${appState.selectedConversationId}/messages`, { method: "POST", body: JSON.stringify({ content }) });
    input.value = "";
    await loadConversations();
  } catch (error) { notify(document.querySelector("#social-chat-message"), error.message); }
  finally { button.disabled = false; }
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
  results.innerHTML = Array.from({ length: 6 }, () => '<article class="skeleton-card" aria-hidden="true"><div class="skeleton-photo"></div><div class="skeleton-copy"></div><div class="skeleton-copy short"></div></article>').join("");
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
    container.innerHTML = emptyState("Chưa tìm thấy hồ sơ phù hợp", "Hãy thử điều chỉnh bộ lọc hoặc quay lại sau.", "Điều chỉnh bộ lọc", "discover");
    return;
  }
  container.innerHTML = profiles.map((profile) => {
    const name = cleanProfileName(profile.name);
    const avatar = profile.photos?.find((photo) => photo.isAvatar) || profile.photos?.[0];
    const image = photoUrl(avatar?.url);
    const city = profile.city ? `<span class="person-card-city">${escapeHtml(profile.city)}</span>` : "";
    return `<article class="person-card" data-profile-id="${profile.id}" tabindex="0">
      ${image ? `<img class="person-photo" data-view-profile="${profile.id}" src="${escapeHtml(image)}" alt="Xem hồ sơ ${escapeHtml(name)}">` : `<div class="person-photo person-photo-placeholder" data-view-profile="${profile.id}" aria-hidden="true">${escapeHtml(name.trim().charAt(0).toUpperCase())}</div>`}
      <div class="person-card-overlay"><div class="person-card-heading"><h2 data-view-profile="${profile.id}">${escapeHtml(name)}</h2><span>${ageFrom(profile.birthDate)}</span></div><div class="person-card-city-row">${city}</div></div>
      <div class="person-card-body"><div class="person-meta"><span>${escapeHtml(lookingForLabel(profile.lookingFor))}</span></div><p class="person-bio">${escapeHtml(profile.bio || "Chưa có phần giới thiệu.")}</p><div class="person-interests">${(profile.interests || []).map((interest) => `<span class="interest-chip">${escapeHtml(interest.name)}</span>`).join("")}</div>
      ${allowSwipe ? `<div class="person-card-actions"><button class="card-round-action pass" type="button" data-swipe="false" aria-label="Bỏ qua">Bỏ qua</button><button class="card-round-action like" type="button" data-swipe="true" aria-label="Kết đôi">Kết đôi ♥</button><button class="card-round-action info" type="button" data-view-profile="${profile.id}" aria-label="Xem hồ sơ">Xem hồ sơ</button></div>` : allowChat ? `<div class="person-card-actions person-card-chat-actions"><button class="person-card-chat-button" type="button" data-open-chat="${profile.id}" aria-label="Nhắn tin với ${escapeHtml(name)}"><svg aria-hidden="true" viewBox="0 0 20 20"><path d="M3 4.75A2.75 2.75 0 0 1 5.75 2h8.5A2.75 2.75 0 0 1 17 4.75v5.5A2.75 2.75 0 0 1 14.25 13H9l-4.5 3v-3.2A2.75 2.75 0 0 1 3 10.25z"/></svg>Nhắn tin</button></div>` : ""}</div></article>`;
  }).join("");
}

function viewProfileGallery(profile) {
  const photos = (profile.photos || []).slice().sort((left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0));
  if (!photos.length) return `<div class="view-profile-photo view-profile-photo-placeholder" aria-hidden="true">${escapeHtml(cleanProfileName(profile.name).trim().charAt(0).toUpperCase())}</div>`;
  return `<div class="view-profile-gallery">${photos.map((photo) => `<img class="view-profile-photo" src="${escapeHtml(photoUrl(photo.url))}" alt="Ảnh hồ sơ ${escapeHtml(profile.name)}">`).join("")}</div>`;
}

function renderViewedProfile(profile, connections, matches) {
  const page = document.querySelector("#view-profile-content");
  const name = cleanProfileName(profile.name);
  const connection = connections.find((item) => item.user?.id === profile.id);
  const matched = matches.some((item) => item.profile?.id === profile.id);
  const connectionAction = connection?.status === "Accepted"
    ? `<button class="profile-action secondary" type="button" disabled>Đã kết nối</button>`
    : connection?.status === "Pending"
      ? `<button class="profile-action secondary" type="button" disabled>Đã gửi lời mời</button>`
      : `<button class="profile-action secondary" type="button" data-view-connect="${profile.id}">Kết nối</button>`;
  const chatAction = matched
    ? `<button class="profile-action" type="button" data-view-chat="${profile.id}">Nhắn tin</button>`
    : `<button class="profile-action" type="button" disabled title="Chỉ nhắn tin sau khi đã kết đôi">Nhắn tin</button>`;
  const interests = (profile.interests || []).map((interest) => `<span class="interest-chip">${escapeHtml(interest.name)}</span>`).join("");
  page.innerHTML = `<button class="profile-back-button" type="button" data-view-back>← Quay lại</button>
    <div class="view-profile-hero">${viewProfileGallery(profile)}<div class="view-profile-intro"><p class="eyebrow">HỒ SƠ CÔNG KHAI</p><h1>${escapeHtml(name)}, ${escapeHtml(ageFrom(profile.birthDate))}</h1><p class="view-profile-location">${escapeHtml([profile.city, profile.gender].filter(Boolean).join(" · ") || "Chưa cập nhật")}</p><div class="view-profile-actions"><button class="profile-action primary" type="button" data-view-swipe="${profile.id}">♡ Kết đôi</button>${connectionAction}${chatAction}</div></div></div>
    <div class="view-profile-details"><section class="view-profile-card"><h2>Về ${escapeHtml(name)}</h2><p>${escapeHtml(profile.bio || "Chưa có phần giới thiệu.")}</p></section><section class="view-profile-card"><h2>Thông tin</h2><dl class="profile-facts">${profile.occupation ? `<div><dt>Công việc</dt><dd>${escapeHtml(profile.occupation)}</dd></div>` : ""}${profile.education ? `<div><dt>Học vấn</dt><dd>${escapeHtml(profile.education)}</dd></div>` : ""}<div><dt>Mục đích</dt><dd>${escapeHtml(lookingForLabel(profile.lookingFor))}</dd></div></dl></section>${interests ? `<section class="view-profile-card"><h2>Sở thích</h2><div class="person-interests">${interests}</div></section>` : ""}</div>`;
}

async function loadViewProfile(profileId) {
  const loading = document.querySelector("#view-profile-loading");
  const errorBox = document.querySelector("#view-profile-error");
  const content = document.querySelector("#view-profile-content");
  showPage("view-profile");
  loading.hidden = false;
  errorBox.hidden = true;
  content.hidden = true;
  try {
    const profile = await request(`/profiles/${encodeURIComponent(profileId)}`);
    let connections = [];
    let matches = [];
    try { [connections, matches] = await Promise.all([request("/connections"), request("/matches")]); } catch { }
    appState.viewedProfile = profile.id;
    renderViewedProfile(profile, connections, matches);
    content.hidden = false;
  } catch (error) {
    errorBox.innerHTML = emptyState(error.status === 404 ? "Không tìm thấy hồ sơ" : "Không thể tải hồ sơ", error.status === 404 ? "Hồ sơ này có thể đã bị xóa hoặc không còn công khai." : "Kiểm tra kết nối mạng rồi thử lại.", "Quay lại Khám phá", "discover");
    errorBox.hidden = false;
  } finally {
    loading.hidden = true;
  }
}

function openProfileFromCard(event) {
  const card = event.target.closest("[data-profile-id]");
  const trigger = event.target.closest("[data-view-profile]");
  if (!card) return;
  if (trigger) {
    loadViewProfile(trigger.dataset.viewProfile || card.dataset.profileId);
    return;
  }
  if (event.target.closest("button, a, input, select, textarea")) return;
  const profileId = card.dataset.profileId;
  if (profileId) loadViewProfile(profileId);
}

async function handleViewedProfileAction(event) {
  const button = event.target.closest("[data-view-swipe], [data-view-connect], [data-view-chat], [data-view-back]");
  if (!button) return;
  if (button.hasAttribute("data-view-back")) {
    showPage("discover");
    return;
  }
  if (button.hasAttribute("data-view-chat")) {
    chatState.pendingUserId = button.dataset.viewChat;
    showPage("chat");
    return;
  }
  button.disabled = true;
  try {
    if (button.hasAttribute("data-view-swipe")) {
      const result = await request("/swipes", { method: "POST", body: JSON.stringify({ targetUserId: button.dataset.viewSwipe, isLike: true }) });
      button.textContent = result.isMatch ? "♥ Đã kết đôi" : "♥ Đã thích";
      if (result.isMatch) await loadMatches();
    } else {
      await request(`/connections/${button.dataset.viewConnect}`, { method: "POST" });
      button.textContent = "Đã gửi lời mời";
    }
  } catch (error) {
    button.disabled = false;
    notify(document.querySelector("#view-profile-error"), error.message);
    document.querySelector("#view-profile-error").hidden = false;
  }
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
    if (button.dataset.swipe === "true") {
      const burst = document.createElement("span");
      burst.className = "heart-burst";
      burst.textContent = "♥";
      card.append(burst);
      setTimeout(() => burst.remove(), 850);
    }
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
  document.querySelector("#chat-layout")?.classList.add("has-active-chat");
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
  document.querySelector("#chat-layout")?.classList.remove("has-active-chat");
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
document.querySelector("#chat-back-to-list").addEventListener("click", () => document.querySelector("#chat-layout")?.classList.remove("has-active-chat"));
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
      feed.innerHTML = emptyState("Hành trình đang chờ bạn", "Chia sẻ một khoảnh khắc nhỏ để bắt đầu câu chuyện.", "Đăng bài đầu tiên", "journey");
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
  stopSocialPolling();
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
  document.querySelector("#discover-results").addEventListener("click", openProfileFromCard);
  document.querySelector("#rooms-filter-form").addEventListener("submit", (event) => { event.preventDefault(); loadRooms(); });
  document.querySelector("#rooms-results").addEventListener("click", (event) => { const button = event.target.closest("[data-room-join]"); if (button) joinRoom(button.dataset.roomJoin); });
  document.querySelector("#create-room-button").addEventListener("click", () => document.querySelector("#create-room-dialog").showModal());
  document.querySelector("#close-room-dialog").addEventListener("click", () => document.querySelector("#create-room-dialog").close());
  document.querySelector("#create-room-form").addEventListener("submit", submitCreateRoom);
  document.querySelector("#create-room-form [name=isPrivate]").addEventListener("change", toggleRoomInvite);
  document.querySelector("#matches-results").addEventListener("click", openChatFromMatch);
  document.querySelector("#matches-results").addEventListener("click", openProfileFromCard);
  document.querySelector("#view-profile-content").addEventListener("click", handleViewedProfileAction);
  document.querySelector("#view-profile-error").addEventListener("click", handleViewedProfileAction);
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
  document.querySelector("#people-search-form").addEventListener("submit", (event) => { event.preventDefault(); loadConnections(event.currentTarget.elements.q.value.trim()); });
  document.querySelector("#connections-page").addEventListener("click", manageConnection);
  document.querySelector("#conversation-list").addEventListener("click", (event) => {
    const button = event.target.closest("[data-open-conversation]");
    if (!button) return;
    appState.selectedConversationId = button.dataset.openConversation;
    renderConversations();
    loadSocialMessages();
  });
  document.querySelector("#social-chat-form").addEventListener("submit", submitSocialMessage);
  document.querySelector("#social-chat-form textarea").addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      event.currentTarget.form.requestSubmit();
    }
  });
  document.querySelectorAll("[data-page]").forEach((button) => button.addEventListener("click", () => showPage(button.dataset.page)));
document.addEventListener("click", (event) => {
  const action = event.target.closest("[data-empty-page]");
  if (action) showPage(action.dataset.emptyPage);
});
  document.querySelector("#logout-button").addEventListener("click", logout);
document.querySelectorAll("#discover-filters input[type=range]").forEach((input) => input.addEventListener("input", () => {
  const form = document.querySelector("#discover-filters");
  const min = Number(form.elements.minAge.value);
  const max = Number(form.elements.maxAge.value);
  if (min > max) {
    if (input.name === "minAge") form.elements.maxAge.value = min;
    else form.elements.minAge.value = max;
  }
  document.querySelector("#age-range-output").value = `${form.elements.minAge.value}–${form.elements.maxAge.value}`;
  document.querySelector("#age-range-output").textContent = `${form.elements.minAge.value}–${form.elements.maxAge.value}`;
}));
document.querySelector("#account-menu-toggle").addEventListener("click", () => {
  const toggle = document.querySelector("#account-menu-toggle");
  const menu = document.querySelector("#account-menu");
  const expanded = toggle.getAttribute("aria-expanded") === "true";
  toggle.setAttribute("aria-expanded", String(!expanded));
  menu.hidden = expanded;
});
document.querySelector("#account-profile-link").addEventListener("click", () => {
  document.querySelector("#account-menu-toggle").setAttribute("aria-expanded", "false");
  document.querySelector("#account-menu").hidden = true;
  showPage("profile");
});
document.addEventListener("click", (event) => {
  if (!event.target.closest(".app-account")) {
    document.querySelector("#account-menu-toggle")?.setAttribute("aria-expanded", "false");
    const menu = document.querySelector("#account-menu");
    if (menu) menu.hidden = true;
  }
});
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
