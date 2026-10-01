import { Page, Locator, expect } from '@playwright/test';

export class ChatPage {
  readonly page: Page;
  readonly messageInput: Locator;
  readonly sendButton: Locator;
  readonly messageBubbles: Locator;
  readonly typingIndicator: Locator;
  readonly modelSelectorButton: Locator;
  readonly changeProviderButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.messageInput = page.locator('textarea[placeholder*="Envie uma mensagem"], textarea[placeholder*="Aguardando resposta"], textarea[placeholder*="Envie uma mensagem para"]');
    this.sendButton = page.locator('button:has(svg.lucide-send-horizontal)');
    this.messageBubbles = page.locator('.group.flex.gap-3.py-4');
    this.typingIndicator = page.locator('text=Processando...');
    this.modelSelectorButton = page.locator('button:has(svg.lucide-chevron-down)').first();
    this.changeProviderButton = page.locator('text=Alterar Provedor');
  }

  /**
   * Navega para a página de chat principal (Geral).
   */
  async goto() {
    await this.page.goto('/');
  }

  /**
   * Navega para um chat dedicado a um agente específico.
   */
  async gotoAgentChat(agentName: string) {
    await this.page.goto(`/chat/${encodeURIComponent(agentName)}`);
  }

  /**
   * Envia uma mensagem no chat.
   */
  async sendMessage(message: string) {
    await this.messageInput.waitFor({ state: 'visible' });
    await this.messageInput.fill(message);
    await this.sendButton.waitFor({ state: 'visible' });
    await this.sendButton.click();
  }

  /**
   * Retorna os textos de todas as mensagens renderizadas no chat.
   */
  async getMessages(): Promise<string[]> {
    const bubbles = this.messageBubbles.locator('.chat-markdown, p.whitespace-pre-wrap');
    await bubbles.first().waitFor({ state: 'attached', timeout: 5000 }).catch(() => {});
    const count = await bubbles.count();
    const texts: string[] = [];
    for (let i = 0; i < count; i++) {
      const text = await bubbles.nth(i).innerText();
      texts.push(text.trim());
    }
    return texts;
  }

  /**
   * Retorna informações dos badges de tier dos agentes.
   */
  async getAgentBadges(): Promise<{ agentName: string; tierLabel: string }[]> {
    const badgesLocator = this.page.locator('.flex.items-center.gap-2.mb-1');
    const count = await badgesLocator.count();
    const badges: { agentName: string; tierLabel: string }[] = [];
    for (let i = 0; i < count; i++) {
      const parent = badgesLocator.nth(i);
      const name = await parent.locator('.text-zinc-300').innerText().catch(() => '');
      const tier = await parent.locator('span[class*="rounded font-medium"]').innerText().catch(() => '');
      badges.push({
        agentName: name.trim(),
        tierLabel: tier.trim(),
      });
    }
    return badges;
  }

  /**
   * Verifica se o indicador de digitação (typing indicator) está visível.
   */
  async isTypingIndicatorVisible(): Promise<boolean> {
    return await this.typingIndicator.isVisible();
  }

  /**
   * Aguarda até que o indicador de digitação suma (processamento concluído).
   */
  async waitForResponse(timeoutMs: number = 30000) {
    // Aguarda o indicador aparecer e depois sumir
    try {
      await this.typingIndicator.waitFor({ state: 'visible', timeout: 2000 });
    } catch (e) {
      // Caso a resposta seja instantânea ou o typing indicator não tenha aparecido a tempo
    }
    await this.typingIndicator.waitFor({ state: 'hidden', timeout: timeoutMs });
  }

  /**
   * Altera o modelo de LLM ativo no chat através da pílula de seleção.
   */
  async selectModel(provider: string, model: string) {
    await this.modelSelectorButton.click();
    
    // Se o provedor atual for diferente do desejado, altera o provedor
    const activeProviderText = await this.page.locator('text=Ativo:').innerText().catch(() => '');
    if (!activeProviderText.toUpperCase().includes(provider.toUpperCase())) {
      await this.changeProviderButton.click();
      await this.page.locator(`button:has-text("${provider}")`).click();
    }
    
    // Clica no modelo correspondente
    await this.page.locator(`button:has-text("${model}")`).first().click();
  }

  /**
   * Retorna as citações/fontes de RAG associadas a mensagens de bot.
   */
  async getCitations(): Promise<{ sourceDocumentName: string; content?: string }[]> {
    const citationsButton = this.page.locator('button[aria-label="Ver citação"]');
    const count = await citationsButton.count();
    const citations: { sourceDocumentName: string; content?: string }[] = [];
    
    for (let i = 0; i < count; i++) {
      const button = citationsButton.nth(i);
      const labelText = await button.innerText();
      // Remove colchetes de índice como [1]
      const cleanName = labelText.replace(/\[\d+\]\s*/, '').trim();
      
      citations.push({
        sourceDocumentName: cleanName,
      });
    }
    return citations;
  }
}
